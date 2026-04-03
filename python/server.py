from flask import Flask, request, jsonify

app = Flask(__name__)

app.config["JSON_AS_ASCII"] = False
app.config["JSONIFY_MIMETYPE"] = "application/json;charset=utf-8"

@app.route("/unity",methods = ["POST"])
def unity_api():
    data = request.get_json()
    data_runState = data.get("runState")

    if not data.get("isParamValid",False):
        return jsonify({
            "action": "确认参数",
            "reply": "请先确认参数"
        })
    
    match data_runState:
        case "MainMenu" : 
            return jsonify({
                "action" : "选择实验",
                "reply" : "在主菜单中选择下一步的实验"
        })
        case "FirstExp" :
            return jsonify({
                "action" : "实验一开始",
                "reply" : "开始操作第一个实验"
            })
        case "SecondExp" : 
            return jsonify({
                "action" : "实验二开始",
                "reply" : "开始操作第二个实验"
            })
        case "Summary" :
            return jsonify({
                "action" : "总结阶段",
                "reply" : "开始进行所有实验的总结"
            })
    # return jsonify({
    #     "action": "跳转到下一步",
    #     "reply": "可以进行下一步"
    #     })
    #print("Unity发来:",data)

    #msg_from_unity = data.get("msg","")
    #reply = f"Python已收到:{msg_from_unity}"
   

if __name__ == "__main__":
    app.run(host = "0.0.0.0", port = 5000,debug = True)