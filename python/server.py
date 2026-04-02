from flask import Flask, request, jsonify

app = Flask(__name__)

app.config["JSON_AS_ASCII"] = False
app.config["JSONIFY_MIMETYPE"] = "application/json;charset=utf-8"

@app.route("/unity",methods = ["POST"])
def unity_api():
    data = request.get_json()
    print("Unity发来:",data)

    msg_from_unity = data.get("msg","")
    reply = f"Python已收到:{msg_from_unity}"
    return jsonify({
        "code":200,
        "msg":reply
        })

if __name__ == "__main__":
    app.run(host = "0.0.0.0", port = 5000,debug = True)