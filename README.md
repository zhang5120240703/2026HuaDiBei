<h1>使用规范</h1>

<h2>⚠️ 最重要规则（必看）</h2>
<ul>
  <li>负责 Python 的人 <strong>只允许修改 Python/ 文件夹</strong>，不要动任何 Unity 文件</li>
  <li>负责 Unity 的人 <strong>只允许修改 Unity 项目文件</strong>，不要动 Python/ 文件夹</li>
  <li>每次提交写清楚本次提交修改的内容，不要写一堆什么111aaaa的乱码上来</li>
  <li>双方互不干扰，绝对不会冲突、不会覆盖！</li>
</ul>
<p style="color:red; font-size:16px; font-weight:bold;">【特别重要】禁止多人同时编辑同一场景文件！
在修改任意场景前，务必先拉取最新代码并与队友沟通同步，否则极易造成场景冲突、内容丢失或损坏，且难以修复。</p>

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

<hr>

<h2>六、Git LFS 使用说明（大文件管理）</h2>
<p>Git LFS 用于管理 Unity 项目中的大文件（模型、图片、音频、视频、预制体等），必须安装使用。</p>

<p>1. 安装 Git LFS（第一次使用）</p>
<pre>git lfs install</pre>

<p>2. 拉取 LFS 大文件（克隆后执行一次）</p>
<pre>git lfs pull</pre>

<p>3. 日常提交大文件（和普通提交一样）</p>
<pre>
git add .
git commit -m "feat: 新增模型资源"
git push
</pre>

<p>4. 查看 LFS 跟踪状态</p>
<pre>git lfs status</pre>

<p>5. 查看当前 LFS 已跟踪的大文件</p>
<pre>git lfs ls-files</pre>

<p>6. 手动追踪指定类型大文件（示例）</p>
<pre>
git lfs track "*.fbx"
git lfs track "*.png"
git lfs track "*.jpg"
git lfs track "*.psd"
git lfs track "*.mp3"
git lfs track "*.wav"
git lfs track "*.prefab"
</pre>

<p><strong>注意：</strong> 克隆项目后如果模型/图片丢失/无法打开，请执行 git lfs pull。</p>
# 单摆物理虚拟实验

## 基础操作
- 鼠标拖动摆球
  - 左键按住摆球拖动
  - 系统会自动限制最大摆角（默认 45°）
  - 松开鼠标 → 摆球开始自由摆动

- 调节摆长
  - 使用界面上的摆长滑动条
  - 范围：0.5m ~ 2m
  - 摆长改变后，摆球与摆线会自动刷新

- 开始周期计数
  - 松开摆球后自动开始计数
  - 界面实时显示：周期数、10 周期总时间、理论周期

---

## 实验流程
1. 调整目标摆长
2. 拖动摆球到合适角度（小角度更准确）
3. 松开摆球，系统自动计数周期
4. 完成一组实验后，记录数据
5. 更换摆长，重复实验（共 3 组）
6. 查看最终平均重力加速度 g
7. 点击重置按钮可重新开始实验

---

## 界面显示说明
- 周期数：已完成的完整摆动周期
- 10 周期总时间：前 10 个周期的总耗时
- 理论周期：基于 g=9.8 计算的标准周期
- 平均周期：所有周期的平均值
- 动能 / 势能 / 总机械能：实时能量数据
- 实验判定：自动计算 g 并给出是否合格
