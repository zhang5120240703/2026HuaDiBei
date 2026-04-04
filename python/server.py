from flask import Flask, jsonify, request
import requests
import json
import os

app = Flask(__name__)

app.config["JSON_AS_ASCII"] = False
app.config["JSONIFY_MIMETYPE"] = "application/json;charset=utf-8"

MODEL_API_KEY = os.getenv("MODEL_API_KEY","")
MODEL_API_URL = os.getenv("MODEL_API_URL","")
MODEL_NAME = os.getenv("MODEL_NAME","")

# FLOW_RULES = {
#     "MainMenu": {
#         "StartProgram": {
#             "action": "ChooseExp",
#             "nextState": "FirstExp",
#             "nextStep": "RunFirstExp",
#             "reply": "选择实验。",
#         },
#     },
#     "FirstExp": {
#         "RunFirstExp": {
#             "action": "StartFirstExp",
#             "nextState": "SecondExp",
#             "nextStep": "RunSecondExp",
#             "reply": "开始实验一。",
#         },
#     },
#     "SecondExp": {
#         "RunSecondExp": {
#             "action": "StartSecondExp",
#             "nextState": "Summary",
#             "nextStep": "FinishSummary",
#             "reply": "开始实验二。",
#         },
#     },
#     "Summary": {
#         "FinishSummary": {
#             "action": "EnterSummary",
#             "nextState": "Summary",
#             "nextStep": "Completed",
#             "reply": "进入总结阶段。",
#         },
#         "Completed": {
#             "action": "NoAction",
#             "nextState": "Summary",
#             "nextStep": "Completed",
#             "reply": "流程已完成，当前没有可执行动作。",
#         },
#     },
# }

TRANSITIONS = {
    ("MainMenu", "ChooseExp"):("FirstExp", "RunFirstExp"),
    ("FirstExp", "StartFirstExp"):("SecondExp","RunSecondExp"),
    ("SecondExp", "StartSecondExp"):("Summary","FinishSummary"),
    ("Summary","EnterSummary"):("Summary","Completed"),
}

VALID_STEPS = {
    "MainMenu": {"StartProgram"},
    "FirstExp": {"RunFirstExp"},
    "SecondExp": {"RunSecondExp"},
    "Summary": {"FinishSummary", "Completed"},
}

# 统一构造返回给 Unity 的 JSON 数据，避免每个分支重复写相同结构。
def build_response(action: str, next_state: str, next_step: str, reply: str):
    return jsonify(
        {
            "action": action,
            "nextState": next_state,
            "nextStep": next_step,
            "reply": reply,
        }
    )


def is_known_step(run_state: str, current_step: str) -> bool:
    return current_step in VALID_STEPS.get(run_state, set())


def resolve_transition(run_state: str, current_step: str, action: str):
    if action == "NoAction":
        return run_state, current_step

    return TRANSITIONS.get((run_state, action))


# 接收 Unity 发送的状态数据，根据当前状态、当前步骤和允许动作返回对应结果。
@app.route("/unity", methods=["POST"])
def unity_api():
    data = request.get_json(silent=True) or {}
    run_state = data.get("runState", "MainMenu")
    current_step = data.get("currentStep", "")
    available_actions = data.get("availableActions", [])

    if not isinstance(available_actions, list):
        available_actions = []

    if run_state not in VALID_STEPS:
        return build_response(
            action="NoAction",
            next_state=run_state,
            next_step=current_step,
            reply=f"未知状态：{run_state}",
        )

    if not is_known_step(run_state, current_step):
        return build_response(
            action="NoAction",
            next_state=run_state,
            next_step=current_step,
            reply=f"状态 {run_state} 下没有步骤 {current_step}",
        )

    # 参数不合法时，不推进流程，只返回 NoAction。
    if not data.get("isParamValid", False):
        return build_response(
            action="NoAction",
            next_state=run_state,
            next_step=current_step,
            reply="当前参数未确认，不执行动作。",
        )

    if not available_actions:
        return build_response(
            action="NoAction",
            next_state=run_state,
            next_step=current_step,
            reply="当前阶段没有可执行动作。",
        )

    try:
        action,reply = ask_model(run_state,current_step,available_actions)
    except Exception as exc:
        return build_response(
            action="NoAction",
            next_state=run_state,
            next_step=current_step,
            reply=f"模型调用失败：{exc}",
        )

    model_action = action
    if model_action != "NoAction" and action not in available_actions:
        action = "NoAction"
        reply = f"模型返回非法动作：{model_action}，允许动作：{available_actions}"

    next_flow = resolve_transition(run_state, current_step, action)
    if next_flow is None:
        return build_response(
            action="NoAction",
            next_state=run_state,
            next_step=current_step,
            reply=f"本地未定义动作 {action} 的状态流转",
        )

    next_state,next_step = next_flow
    return build_response(
        action, 
        next_state, 
        next_step, 
        reply
        )

    # # 状态未知时，返回兜底结果。
    # state_rules = FLOW_RULES.get(run_state)
    # if state_rules is None:
    #     return build_response(
    #         action="NoAction",
    #         next_state=run_state,
    #         next_step=current_step,
    #         reply=f"未知状态: {run_state}",
    #     )

    # # 同一个大状态下，再根据 currentStep 决定具体动作。
    # rule = state_rules.get(current_step)
    # if rule is None:
    #     return build_response(
    #         action="NoAction",
    #         next_state=run_state,
    #         next_step=current_step,
    #         reply=f"状态 {run_state} 下没有步骤 {current_step}",
    #     )

    # # Python 侧也校验返回动作是否在 Unity 给出的允许列表里。
    # if rule["action"] != "NoAction" and rule["action"] not in available_actions:
    #     return build_response(
    #         action="NoAction",
    #         next_state=run_state,
    #         next_step=current_step,
    #         reply=f"动作 {rule['action']} 不在允许列表中",
    #     )

    # return build_response(
    #     action=rule["action"],
    #     next_state=rule["nextState"],
    #     next_step=rule["nextStep"],
    #     reply=rule["reply"],
    # )

def ask_model(run_state,current_step,available_actions):
    promot = f"""
你是一个高中物理实验教学的Unity内置AI辅助教学助手，
当前状态：{run_state}
当前步骤：{current_step}
允许动作：{available_actions}

你只能从允许动作里选一个动作作为一个action。
如果没有合适的动作就返回NoAction。
只返回json，不要返回其他内容
格式：
{{"action":"NoAction","reply":"..."}}
"""
    raw_text = call_your_model_api(promot)
    data = json.loads(raw_text)
    action = data.get("action","NoAction") or "NoAction"
    reply = data.get("reply","") or ""
    return action,reply


def call_your_model_api(prompt:str)->str:
    if not MODEL_API_KEY:
        raise RuntimeError("没有设置API_KEY")
    if not MODEL_API_URL:
        raise RuntimeError("没有设置API_URL")
    if not MODEL_NAME:
        raise RuntimeError("没有设置MODEL_NAME")
    
    #构建请求头
    headers = {
        "Authorization":f"Bearer {MODEL_API_KEY}",#API密钥
        "Content-Type":"application/json",#告诉服务器发送的JSON格式
    }
    payload = {
        "model":MODEL_NAME,
        "messages":[
            {
                "role":"system",
                "content":(
                    "你只能返回JSON,不要返回解释，不要返回markdown代码块"
                    "你必须只从用户给出的 allowed actions 里选择 action"
                    "输出格式必须是："
                    "{\"action\":\"NoAction\",\"reply\":\"...\"}"
                ),
            },
            #用户真正的提问
            {
                "role":"user",
                "content":prompt,
            },
        ],
        "response_format" : {"type":"json_object"},
        "temperature":0,#稳定不随机创造性为0
        "max_tokens":200,

    }

    # 发送post到API
    response = requests.post(
        MODEL_API_URL,
        headers = headers,
        json = payload,
        timeout = 30,
    )

    print("Deepseek Status:",response.status_code)
    print("Deepseek原始回复：")
    print(response.text)
    #检查请求是否成功
    response.raise_for_status()

    #解析返回的JSON
    result = response.json()
    print("DeepseekJSON参数:")
    #返回的JSON格式化后打出
    print(json.dumps(result,ensure_ascii=False,indent=2))
    content = result["choices"][0]["message"]["content"]
    #
    print("Deepseek Content:")
    print(content)

    if not content:
        return '{"action":"NoAction","reply":"模型返回空内容"}'
    content = content.strip()

    if content.startswith("```json"):
        content = content[7:]
    elif content.startswith("```"):
        content = content[3:]

    if content.endswith("```"):
        content = content[:-3]

    return content.strip()


# 启动 Flask 本地服务，供 Unity 通过 HTTP POST 调用。
if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=True)
