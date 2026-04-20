/// <summary>
/// 交互模块基类（原型框架）
/// 所有后续交互模块必须继承此类
/// 为拓展奠定基础
/// </summary>
public abstract class InteractionModuleBase
{
    protected string ModuleName { get; private set; }
    protected bool IsModuleEnabled { get; private set; }

    protected InteractionModuleBase(string moduleName)
    {
        ModuleName = moduleName;
        IsModuleEnabled = false;
    }

    // 初始化模块
    public virtual void InitModule()
    {
        IsModuleEnabled = true;
    }

    // 关闭模块
    public virtual void DisableModule()
    {
        IsModuleEnabled = false;
    }

    // 模块执行逻辑
    public abstract void ExecuteModuleLogic();
}

/// <summary>
/// 参数交互模块（原型）
/// </summary>
public class ParamInteractionModule : InteractionModuleBase
{
    public ParamInteractionModule() : base("ParamInteraction") { }

    public override void ExecuteModuleLogic()
    {
        // 参数修改、校验逻辑
        // 留给后续拓展
    }
}

/// <summary>
/// 实验控制交互模块（原型）
/// </summary>
public class ControlInteractionModule : InteractionModuleBase
{
    public ControlInteractionModule() : base("ExperimentControl") { }

    public override void ExecuteModuleLogic()
    {
        // 启动/暂停/重置 拓展逻辑
    }
}