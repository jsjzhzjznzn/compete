/// <summary>
/// 连击状态类型枚举（网络同步标识）
/// 序列化顺序即枚举值顺序：新增状态只能追加在末尾，禁止调整/删除已有值，
/// 否则会导致新老客户端状态错位。
/// </summary>
public enum ComboStateType
{
    Null,
    Attacking,
    Skill
}
