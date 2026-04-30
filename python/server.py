from flask import Flask, jsonify, request
import requests
import json
import os

app = Flask(__name__)

app.config["JSON_AS_ASCII"] = False
app.config["JSONIFY_MIMETYPE"] = "application/json;charset=utf-8"

MODEL_API_KEY = os.getenv("MODEL_API_KEY", "")
MODEL_API_URL = os.getenv("MODEL_API_URL", "")
MODEL_NAME = os.getenv("MODEL_NAME", "")

# 当前已启用的请求类型
REQUEST_TYPE_MESSAGE = "message"
REQUEST_TYPE_SUMMARY = "summary"

# 预留的请求类型，后面扩功能时直接沿用
REQUEST_TYPE_GUIDANCE = "guidance" # 实验指导
REQUEST_TYPE_QA = "qa"  # 实验问答
REQUEST_TYPE_REPORT = "report" # 实验报告

# 当前已启用的响应类型
RESPONSE_TYPE_MESSAGE = "message"
RESPONSE_TYPE_SUMMARY = "summary"
RESPONSE_TYPE_ERROR = "error"


def normalize_text(value) -> str:
    """把传入值统一转成去首尾空格后的字符串。"""
    return str(value or "").strip()


def build_response(
    response_type: str, # 响应类型
    reply: str, # 模型回复的主要内容
    suggestion: str = "", # 建议
    extra_data_json: str = "", # 预留的额外数据字段
):
    """统一响应结构，给 Unity 一个稳定的协议。"""
    return jsonify(
        {
            "responseType": response_type,
            "reply": reply,
            "suggestion": suggestion,
            "extraDataJson": extra_data_json,
        }
    )


def parse_request_body():
    """统一解析 Unity 请求字段，方便后面继续扩展。"""
    data = request.get_json(silent=True) or {}
    return {
        "request_type": normalize_text(data.get("requestType")).lower(),
        "experiment_name": normalize_text(data.get("experimentName")),
        "content": normalize_text(data.get("content")),
        "summary_context": normalize_text(data.get("summaryContext")),
        "extra_context": normalize_text(data.get("extraContext")),
        "metadata_json": normalize_text(data.get("metadataJson")),
    }


@app.route("/unity", methods=["POST"])
def unity_api():
    request_data = parse_request_body()
    request_type = request_data["request_type"]

    # 用处理函数分发请求，后面新增功能时只需要补 handler
    handlers = {
        REQUEST_TYPE_MESSAGE: handle_message_request,
        REQUEST_TYPE_SUMMARY: handle_summary_request,
    }

    handler = handlers.get(request_type)
    if handler is None:
        return build_response(RESPONSE_TYPE_ERROR, "未知请求类型。")

    try:
        return handler(request_data)
    except Exception as exc:
        return build_response(RESPONSE_TYPE_ERROR, f"AI 调用失败：{exc}")


def handle_message_request(request_data: dict):
    """处理普通消息请求，例如实验场景中的实时提问。"""
    content = request_data["content"]
    if not content:
        return build_response(RESPONSE_TYPE_ERROR, "消息内容为空。")

    reply = ask_message_model(
        experiment_name=request_data["experiment_name"],
        content=content,
        extra_context=request_data["extra_context"],
        metadata_json=request_data["metadata_json"],
    )

    return build_response(RESPONSE_TYPE_MESSAGE, reply)


def handle_summary_request(request_data: dict):
    """处理实验总结请求。"""
    summary_context = request_data["summary_context"]
    if not summary_context:
        return build_response(RESPONSE_TYPE_ERROR, "实验总结内容为空。")

    reply = ask_summary_model(
        experiment_name=request_data["experiment_name"],
        summary_context=summary_context,
        extra_context=request_data["extra_context"],
        metadata_json=request_data["metadata_json"],
    )

    return build_response(RESPONSE_TYPE_SUMMARY, reply)


def ask_message_model(
    experiment_name: str,
    content: str,
    extra_context: str = "",
    metadata_json: str = "",
) -> str:
    """生成普通问答回复。"""
    experiment_display_name = experiment_name or "当前实验"

    system_prompt = (
        "你是一个高中物理虚拟实验教学助手。"
        "请使用简体中文回答，回答要准确、清晰、简洁。"
        "如果用户提供的信息不足，不要编造实验数据，应直接指出还缺什么信息。"
    )

    user_prompt = (
        f"实验名称：{experiment_display_name}\n"
        f"用户消息：{content}\n"
        f"补充上下文：{extra_context or '无'}\n"
        f"结构化数据：{metadata_json or '无'}\n\n"
        "请直接回答用户问题，不要输出 JSON，不要输出 markdown 代码块。"
    )

    return call_your_model_api(system_prompt, user_prompt)


def ask_summary_model(
    experiment_name: str,
    summary_context: str,
    extra_context: str = "",
    metadata_json: str = "",
) -> str:
    """生成实验总结。"""
    experiment_display_name = experiment_name or "当前实验"

    system_prompt = (
        "你是一个高中物理虚拟实验教学助手。"
        "你需要根据给出的实验信息生成实验总结。"
        "请使用简体中文，结构清晰，语言自然，不要输出 JSON，不要输出 markdown 代码块。"
    )

    user_prompt = (
        f"实验名称：{experiment_display_name}\n"
        f"实验信息：\n{summary_context}\n\n"
        f"补充上下文：{extra_context or '无'}\n"
        f"结构化数据：{metadata_json or '无'}\n\n"
        "请生成一份实验总结，内容至少包含：\n"
        "1. 实验目的\n"
        "2. 实验过程概括\n"
        "3. 实验结果与结论\n"
        "4. 存在的问题\n"
        "5. 改进建议"
    )

    return call_your_model_api(system_prompt, user_prompt)


def call_your_model_api(system_prompt: str, user_prompt: str) -> str:
    """统一调用 DeepSeek 接口。"""
    if not MODEL_API_KEY:
        raise RuntimeError("没有设置 MODEL_API_KEY")
    if not MODEL_API_URL:
        raise RuntimeError("没有设置 MODEL_API_URL")
    if not MODEL_NAME:
        raise RuntimeError("没有设置 MODEL_NAME")

    headers = {
        "Authorization": f"Bearer {MODEL_API_KEY}",
        "Content-Type": "application/json",
    }

    payload = {
        "model": MODEL_NAME,
        "messages": [
            {
                "role": "system",
                "content": system_prompt,
            },
            {
                "role": "user",
                "content": user_prompt,
            },
        ],
        "temperature": 0.3,
        "max_tokens": 800,
    }

    response = requests.post(
        MODEL_API_URL,
        headers=headers,
        json=payload,
        timeout=30,
    )

    print("DeepSeek Status:", response.status_code)
    print("DeepSeek 原始回复：")
    print(response.text)

    response.raise_for_status()

    result = response.json()
    print("DeepSeek JSON 参数：")
    print(json.dumps(result, ensure_ascii=False, indent=2))

    content = result["choices"][0]["message"]["content"]
    print("DeepSeek Content:")
    print(content)

    if not content:
        raise RuntimeError("模型返回空内容")

    content = content.strip()

    if content.startswith("```json"):
        content = content[7:]
    elif content.startswith("```"):
        content = content[3:]

    if content.endswith("```"):
        content = content[:-3]

    return content.strip()


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=True)
