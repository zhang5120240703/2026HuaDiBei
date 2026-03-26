<h1>使用规范</h1>

<h2>一、Git 基础操作</h2>
<p>1. 克隆项目（第一次下载）</p>
<pre>git clone https://github.com/zhang5120240703/2026HuaDiBei</pre>

<p>2. 拉取最新代码（每次开始写代码前）</p>
<pre>git pull</pre>

<p>3. 提交并推送代码</p>
<pre>
git add .
git commit -m "提交信息"
git push
</pre>

<hr>

<h2>二、冲突解决（VS 内操作）</h2>
<p>拉取代码出现冲突时：</p>
<ol>
  <li>在 VS 的 Git 窗口找到冲突文件</li>
  <li>双击打开文件</li>
  <li>选择 <strong>保留本地（自己的代码）</strong> 或 <strong>保留远程（别人的代码）</strong></li>
  <li>保存文件</li>
  <li>重新提交推送</li>
</ol>

<hr>

<h2>三、文件命名规范（必须遵守）</h2>
<ul>
  <li>文件夹名：每个单词首字母大写，其余小写，没有空格，例：Scripts、UI、Models</li>
  <li>C# 脚本：每个单词首字母大写，其余小写，没有空格，例：PlayerController、UIManager</li>
  <li>预制体/资源：每个单词首字母大写，其余小写，没有空格，例：Player、BtnStart</li>
  <li>禁止中文、空格、特殊符号</li>
</ul>

<hr>

<h2>四、提交命名规范（Commit 格式）</h2>
<ul>
  <li>feat: 新增功能（例：feat: 新增玩家移动）</li>
  <li>fix: 修复 Bug（例：fix: 修复碰撞失效）</li>
  <li>ui: 界面修改（例：ui: 调整血条位置）</li>
  <li>opt: 优化代码（例：opt: 优化角色逻辑）</li>
  <li>doc: 修改说明文档</li>
</ul>

<hr>

<h2>五、项目文件夹结构规范</h2>
<ul>
  <li>Art/Models：存放模型文件</li>
  <li>Art/Prefabs：存放预制体文件</li>
  <li>Art/ImportAssets：存放导入的第三方资源</li>
  <li>Art/Animations：存放所有动画文件</li>
  <li>Scripts：存放所有 C# 脚本，按功能分类新建子文件夹</li>
  <li>Scene：存放场景文件</li>
</ul>
