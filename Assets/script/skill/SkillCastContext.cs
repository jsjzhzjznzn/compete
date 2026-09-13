/// <summary>
/// 本次技能释放的一次性运行时上下文。
/// 每次释放独立一份，禁止跨释放复用；节点类里的字段是"配置常量"（启动构建后只读），
/// 会随每次释放变化的东西（动态系数、时间戳、随机种子等）一律放这里。
/// </summary>
public class SkillCastContext
{
    /// <summary>当前释放的技能 ID</summary>
    public int skillId;

    /// <summary>本次释放的动态系数（装备/状态加成带来的变化；1 = 无修正）</summary>
    public float skillCoef = 1f;

    /// <summary>释放时刻（游戏时间，秒）</summary>
    public float executeTime;

    /// <summary>复用前重置（配合对象池用）</summary>
    public void Reset()
    {
        skillId = 0;
        skillCoef = 1f;
        executeTime = 0f;
    }
}
