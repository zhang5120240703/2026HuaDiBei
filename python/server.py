from flask import Flask, jsonify, request

app = Flask(__name__)

app.config["JSON_AS_ASCII"] = False
app.config["JSONIFY_MIMETYPE"] = "application/json;charset=utf-8"

FLOW_RULES = {
    "MainMenu": {
        "StartProgram": {
            "action": "ChooseExp",
            "nextState": "FirstExp",
            "nextStep": "RunFirstExp",
            "reply": "进入实验一。",
        },
    },
    "FirstExp": {
        "RunFirstExp": {
            "action": "StartFirstExp",
            "nextState": "SecondExp",
            "nextStep": "RunSecondExp",
            "reply": "开始实验一。",
        },
    },
    "SecondExp": {
        "RunSecondExp": {
            "action": "StartSecondExp",
            "nextState": "Summary",
            "nextStep": "FinishSummary",
            "reply": "开始实验二。",
        },
    },
    "Summary": {
        "FinishSummary": {
            "action": "EnterSummary",
            "nextState": "Summary",
            "nextStep": "Completed",
            "reply": "进入总结阶段。",
        },
        "Completed": {
            "action": "NoAction",
            "nextState": "Summary",
            "nextStep": "Completed",
            "reply": "流程已完成，当前没有可执行动作。",
        },
    },
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


# 接收 Unity 发送的状态数据，根据当前状态、当前步骤和允许动作返回对应结果。
@app.route("/unity", methods=["POST"])
def unity_api():
    data = request.get_json(silent=True) or {}
    run_state = data.get("runState", "MainMenu")
    current_step = data.get("currentStep", "")
    available_actions = data.get("availableActions", [])

    # 参数不合法时，不推进流程，只返回 NoAction。
    if not data.get("isParamValid", False):
        return build_response(
            action="NoAction",
            next_state=run_state,
            next_step=current_step,
            reply="当前参数未确认，不执行动作。",
        )

    # 状态未知时，返回兜底结果。
    state_rules = FLOW_RULES.get(run_state)
    if state_rules is None:
        return build_response(
            action="NoAction",
            next_state=run_state,
            next_step=current_step,
            reply=f"未知状态: {run_state}",
        )

    # 同一个大状态下，再根据 currentStep 决定具体动作。
    rule = state_rules.get(current_step)
    if rule is None:
        return build_response(
            action="NoAction",
            next_state=run_state,
            next_step=current_step,
            reply=f"状态 {run_state} 下没有步骤 {current_step}",
        )

    # Python 侧也校验返回动作是否在 Unity 给出的允许列表里。
    if rule["action"] != "NoAction" and rule["action"] not in available_actions:
        return build_response(
            action="NoAction",
            next_state=run_state,
            next_step=current_step,
            reply=f"动作 {rule['action']} 不在允许列表中",
        )

    return build_response(
        action=rule["action"],
        next_state=rule["nextState"],
        next_step=rule["nextStep"],
        reply=rule["reply"],
    )


# 启动 Flask 本地服务，供 Unity 通过 HTTP POST 调用。
if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=True)
