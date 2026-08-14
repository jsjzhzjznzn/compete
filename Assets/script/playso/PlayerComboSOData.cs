using UnityEngine;
/// <summary>
/// 命名空间：项目核心资源数据层
/// </summary>
    /// <summary>
    /// 玩家全套连招序列化数据类
    /// 统一存储玩家轻攻击、重攻击、处决、技能、终结技、切换技能所有连招配置资源
    /// 可序列化嵌入PlayerSO，集中管理角色全部战斗连招数据
    /// </summary>
    [System.Serializable]
    public class PlayerComboSOData
    {
        /// <summary>轻攻击连招容器（整套平A连击、闪避攻击数据）</summary>
        [field: SerializeField, Header("轻攻击连招")] 
        public ComboContainerData lightCombo { get; private set; }

        /// <summary>重攻击连招容器（重击整套连击、闪避重击数据）</summary>
        [field: SerializeField, Header("重攻击连招")] 
        public ComboContainerData heavyCombo { get; private set; }

        /// <summary>处决连招容器（倒地/破防后终结处决连招）</summary>
        [field: SerializeField, Header("处决连招")] 
        public ComboContainerData executeCombo { get; private set; }

        /// <summary>常规技能单套连招数据</summary>
        [field: SerializeField, Header("常规技能")] 
        public ComboData skillCombo { get; private set; }

        /// <summary>必杀终结技连招数据</summary>
        [field: SerializeField, Header("终结必杀技")] 
        public ComboData finishSkillCombo { get; private set; }

        /// <summary>形态/武器切换专用技能连招数据</summary>
        [field:SerializeField,Header("切换形态技能")] 
        public ComboData switchSkill { get; private set; }
    }

