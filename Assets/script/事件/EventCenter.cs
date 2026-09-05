using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// 全局事件中心（EventCenter）
// ----------------------------------------------------------------------------
// 【这是什么】
//   一个全局的"发布/订阅"消息总线。不同系统（战斗、技能、连击、Buff、
//   输入、UI）之间不互相引用，只通过事件名收发消息，实现彻底解耦。
//
// 【为什么用枚举 + 泛型容器】
//   - 枚举事件名：编译期就能发现拼写错误，比字符串事件安全
//   - EventContainer<T> 泛型容器：每个事件只绑定一种参数类型，参数类型不匹配
//     在订阅/派发时直接报错，不会运行到一半才崩溃
//   - struct 参数包裹：多个参数塞进一个 struct，字段名即文档，
//     比 CallEvent(float, string, bool, ...) 这种靠顺序猜的可读得多
//
// 【一个事件只能有一种签名】
//   同一个 E_EventType 不能既被"无参"订阅又被"带参"订阅，
//   否则派发时不知道按哪种类型调用——这是设计约束，见 AddListener 的类型检查。
//
// 【线程模型】
//   Unity 逻辑都在主线程，单例不做锁，直接懒加载即可。
// ============================================================================

// ============================================================
// 1. 事件枚举 —— 事件名的唯一清单（编译期类型安全）
// ============================================================
// 新增事件时：在这里加一个枚举值即可，不要用字符串手写。
// 括号里标的是该事件派发时携带的参数类型（对应第 2 节的 struct）。
public enum E_EventType
{
    // ===== 战斗核心 =====
    E_Attack,          // 攻击命中  (HitData)：攻击方打中了谁、打在哪
    E_OnHit,           // 受到攻击  (HitData)：被攻击方知道自己被谁打中（还没扣血）
    E_OnDamage,        // 受到伤害  (DamageData)：实际扣血，血条/飘字/震屏听这个
    E_DamageBlocked,   // 伤害被拦下 (DamageData)：无敌窗口内被打中但不掉血（闪避触发等）
    E_OnDeath,         // 死亡      (DeathData)：生命归零
    E_OnKill,          // 击杀      (无参)：杀死了目标
    E_OnStateChanged,  // 状态切换  (StateChangeData)：移动/连击状态机切状态

    // ===== 技能 =====
    E_SkillCast,       // 释放技能  (SkillData)
    E_SkillCooldownStart, // 技能进入冷却
    E_SkillCooldownEnd,   // 技能冷却结束

    // ===== 连击 =====
    E_ComboChanged,    // 连击数变化 (int)：UI 连击计数

    // ===== Buff =====
    E_BuffAdd,         // 添加 Buff      (BuffChangeData)：UI buff 栏/音效订阅
    E_BuffRemove,      // 移除 Buff      (BuffChangeData)：到期/被清除时派发

    // ===== 输入 =====
    E_InputAttack,       // 按下攻击键
    E_InputSkill1,       // 按下技能1键
    E_InputSkill2,       // 按下技能2键
    E_InputJump,         // 按下跳跃键
    E_InputMoveHorizon,  // 水平输入 (float -1~1)
    E_InputMoveVertical, // 垂直输入 (float -1~1)

    // ===== 系统 =====
    E_RoundStart,      // 回合开始
    E_RoundEnd,        // 回合结束
    E_MatchEnd,        // 整局结束

    // ===== 资源加载 =====
    E_SceneLoadProgress, // 场景加载进度 (float 0~1)
}

// ============================================================
// 2. 事件参数结构体 —— 多参数时用 struct 包裹成一个整体
// ============================================================
// 为什么用 struct 而不是直接传多个参数？
//   - 字段有名字，调用处 new HitData { attacker = ..., target = ... }
//     比传 5 个裸参数一眼看懂
//   - 想加字段就加，订阅方代码不用改（参数列表不变）
//   - struct 是值类型，短小的事件参数在栈上传递，避免堆分配 GC
// 新增事件参数时：在这里建一个 struct，再在枚举注释里标上类型。

/// <summary>一次攻击命中的信息（E_Attack / E_OnHit）</summary>
public struct HitData
{
    public GameObject attacker;   // 谁发起的攻击
    public GameObject target;     // 打中了谁
    public Vector3 hitPoint;      // 命中点世界坐标（播受击特效/飘字用）
}
// 攻击命中 → E_Attack(HitData) → 伤害计算 → E_OnDamage(DamageData) → 飘字/受击
/// <summary>一次伤害结算信息（E_OnDamage / E_DamageBlocked，飘字/受击/闪避订阅）</summary>
public struct DamageData
{
    public GameObject source;     // 伤害来源（可能是施法者，不是直接攻击者）
    public GameObject target;     // 被攻击者（谁掉血了；飘字/受击据此过滤是不是自己）
    public float amount;          // 本次扣血量
    public bool isCritical;       // 是否暴击（飘字/音效可区分）
    public bool isDoT;            // 是否持续伤害（Buff 灼烧类 tick 扣血；受击硬直/闪避触发据此跳过）
}

/// <summary>一次死亡信息（E_OnDeath：谁死了）</summary>
public struct DeathData
{
    public GameObject target;     // 谁死了（复活/掉落/击杀统计用）
}

/// <summary>一次状态机切换信息（E_OnStateChanged）</summary>
public struct StateChangeData
{
    public string fromState;      // 切换前的状态名
    public string toState;        // 切换后的状态名
}

/// <summary>一次技能释放信息（E_SkillCast / 冷却）</summary>
public struct SkillData
{
    public int skillId;           // 技能ID
    public float cooldown;        // 冷却时长（秒）
}

/// <summary>一次 Buff 状态变化信息（E_BuffAdd / E_BuffRemove，UI buff 栏订阅）</summary>
public struct BuffChangeData
{
    public GameObject target;     // 谁身上的 Buff 变了（UI 据此过滤是不是自己）
    public BuffData buffData;     // 哪个 Buff（显示图标/名称用）
    public int stacks;            // 当前层数（移除时为 0）
    public float remainTime;      // 剩余时间（秒，移除时为 0；永久 Buff 为极大值）
}

// ============================================================
// 3. 多态事件容器基类
// ============================================================
// 为什么要这层基类？
//   事件中心内部用 Dictionary<E_EventType, EventContainerBase> 存所有事件，
//   但事件分"无参(EventContainer)"和"带参(EventContainer<T>)"两种，类型不同没法
//   存进同一个字典。基类给了它们一个公共类型，字典就能存了。
//
//   Remove(Delegate)：UnregisterTarget 批量注销时，按一个 Delegate 就能
//   从容器里删订阅，不用知道它是无参还是带参。
public abstract class EventContainerBase
{
    /// <summary>是否有人订阅（调试/清理判断用）</summary>
    public abstract bool HasListeners { get; }

    /// <summary>按委托移除一个订阅（无参/带参容器各自强转实现）</summary>
    public abstract bool Remove(Delegate callback);
}

// ============================================================
// 4. 无参事件容器（E_RoundStart、E_OnKill 这类不带数据的）
// ============================================================
public class EventContainer : EventContainerBase
{
    /// <summary>C# event 委托：多个订阅者会以 += 链式串联</summary>
    private event Action actions;

    public override bool HasListeners => actions != null;

    /// <summary>订阅：+= 可重复订阅（重复订阅会调用两次，注意别加重）</summary>
    public void Add(Action action) => actions += action;

    public override bool Remove(Delegate callback)
    {
        // 基类传进来的是 Delegate，强转为具体的 Action 才能 -= 
        if (callback is Action action)
        {
            actions -= action;
            return true;
        }
        return false;
    }

    /// <summary>触发：?. 表示没人订阅时静默跳过（无参事件没人听是常态，不报错）</summary>
    public void Invoke() => actions?.Invoke();
}

// ============================================================
// 5. 带参事件容器（E_OnDamage、E_Attack 这类带数据的）
// ============================================================
// 泛型 T 就是第 2 节定义的那些 struct。
public class EventContainer<T> : EventContainerBase
{
    private event Action<T> actions;

    public override bool HasListeners => actions != null;

    public void Add(Action<T> action) => actions += action;

    public override bool Remove(Delegate callback)
    {
        if (callback is Action<T> action)
        {
            actions -= action;
            return true;
        }
        return false;
    }

    public void Invoke(T data) => actions?.Invoke(data);
}

// ============================================================
// 6. 事件中心 —— 静态单例（非 MonoBehaviour）
// ============================================================
// 为什么不用 MonoBehaviour？
//   - 事件中心只是"存委托 + 派发"，不需要场景对象、不需要 Update、
//     不需要挂在某个 GameObject 上
//   - 用静态单例省去场景摆放/DontDestroyOnLoad 的麻烦，全局直接用
//   - 初始化在私有构造函数里完成，_eventDict 保证不为 null
//
// 使用惯例：
//   【订阅端】(收到消息的一方，如 UI/血条)
//     OnEnable():
//         EventCenter.MainInstance.AddListener(E_EventType.E_OnDamage, this, OnDamage);
//     OnDisable():
//         EventCenter.MainInstance.UnregisterTarget(this);   // 一键注销本对象所有订阅
//     void OnDamage(DamageData d) { ... }
//
//   【派发端】(发出消息的一方，如战斗/伤害系统)
//     EventCenter.MainInstance.Dispatch(E_EventType.E_OnDamage, new DamageData {
//         source = gameObject, amount = 10, isCritical = false, isDoT = false });
// ============================================================
public class EventCenter
{
    /// <summary>全局唯一实例（静态私有字段）</summary>
    private static EventCenter _mainInstance;

    /// <summary>访问入口（懒加载：第一次用才 new，之后复用）</summary>
    public static EventCenter MainInstance => _mainInstance ??= new EventCenter();

    /// <summary>事件类型 → 容器（一个事件只允许一种参数签名）</summary>
    private readonly Dictionary<E_EventType, EventContainerBase> _eventDict = new Dictionary<E_EventType, EventContainerBase>();

    /// <summary>订阅目标 → 注册记录列表（供 UnregisterTarget 一键注销）</summary>
    /// <remarks>
    /// 这是一个"反向索引"：正常订阅是 事件→回调，这里记的是 对象→(事件,回调)。
    /// 好处：某个对象要销毁时（角色死亡、UI面板关闭），不用逐个 RemoveListener，
    /// 一句 UnregisterTarget(this) 全删掉，杜绝"对象没了但回调还留在事件里"的内存泄漏。
    /// </remarks>
    private readonly Dictionary<object, List<(E_EventType evt, Delegate callback)>> _targetDict =
        new Dictionary<object, List<(E_EventType evt, Delegate callback)>>();

    /// <summary>私有构造：禁止外部 new，单例只能走 MainInstance</summary>
    private EventCenter() { }

    // ==================== 订阅 ====================

    /// <summary>订阅无参数事件（不带订阅目标）</summary>
    public void AddListener(E_EventType evt, Action callback) =>
        AddListener(evt, null, callback);

    /// <summary>订阅无参数事件（记录订阅目标，便于一键注销）</summary>
    /// <param name="evt">事件类型</param>
    /// <param name="target">订阅者对象（通常是 this），可为 null 表示不记录</param>
    /// <param name="callback">回调方法</param>
    public void AddListener(E_EventType evt, object target, Action callback)
    {
        if (_eventDict.TryGetValue(evt, out var info))
        {
            // 事件已存在：必须是同类型（无参）容器，否则说明签名冲突
            if (info is EventContainer eventInfo)
                eventInfo.Add(callback);
            else
                Debug.LogError($"[EventCenter] 事件 {evt} 已注册为带参类型，不能用无参订阅");
        }
        else
        {
            // 事件第一次被订阅：创建无参容器并放进字典
            var eventInfo = new EventContainer();
            eventInfo.Add(callback);
            _eventDict[evt] = eventInfo;
        }
        Track(target, evt, callback);
    }

    /// <summary>订阅带参数事件（不带订阅目标）</summary>
    public void AddListener<T>(E_EventType evt, Action<T> callback) =>
        AddListener(evt, null, callback);

    /// <summary>订阅带参数事件（记录订阅目标，便于一键注销）</summary>
    public void AddListener<T>(E_EventType evt, object target, Action<T> callback)
    {
        if (_eventDict.TryGetValue(evt, out var info))
        {
            // 类型检查：泛型 T 必须和第一次订阅时的类型完全一致，
            // 否则派发时拿到的参数无法安全传给回调 → 直接报错提前暴露问题
            if (info is EventContainer<T> eventInfo)
                eventInfo.Add(callback);
            else
                Debug.LogError($"[EventCenter] 事件 {evt} 参数类型不匹配，" +
                               $"已注册类型 {GetEventArgType(info)}，本次订阅类型 {typeof(T).Name}");
        }
        else
        {
            var eventInfo = new EventContainer<T>();
            eventInfo.Add(callback);
            _eventDict[evt] = eventInfo;
        }
        Track(target, evt, callback);
    }

    // ==================== 取消订阅 ====================

    /// <summary>取消订阅无参数事件（不带订阅目标）</summary>
    public void RemoveListener(E_EventType evt, Action callback) =>
        RemoveListener(evt, null, callback);

    /// <summary>取消订阅无参数事件（同时清掉目标索引里对应记录）</summary>
    public void RemoveListener(E_EventType evt, object target, Action callback)
    {
        if (_eventDict.TryGetValue(evt, out var info))
            info.Remove(callback);
        Untrack(target, evt, callback);
    }

    /// <summary>取消订阅带参数事件（不带订阅目标）</summary>
    public void RemoveListener<T>(E_EventType evt, Action<T> callback) =>
        RemoveListener(evt, null, callback);

    /// <summary>取消订阅带参数事件（同时清掉目标索引里对应记录）</summary>
    public void RemoveListener<T>(E_EventType evt, object target, Action<T> callback)
    {
        if (_eventDict.TryGetValue(evt, out var info))
            info.Remove(callback);
        Untrack(target, evt, callback);
    }

    /// <summary>注销某对象的所有订阅（角色死亡/场景卸载时一句话清理，防内存泄漏）</summary>
    /// <remarks>
    /// 相当于把 this 对象在这个事件中心里的"人脉"全部断掉。
    /// 每个脚本只需在 OnDisable/OnDestroy 里写：
    ///     EventCenter.MainInstance.UnregisterTarget(this);
    /// 不用管自己到底订阅了几个事件。
    /// </remarks>
    public void UnregisterTarget(object target)
    {
        if (target == null || !_targetDict.TryGetValue(target, out var list)) return;

        // 遍历该对象的所有订阅记录，从对应事件容器里逐个移除回调
        foreach (var (evt, callback) in list)
        {
            if (_eventDict.TryGetValue(evt, out var info))
                info.Remove(callback);
        }
        // 清空记录并删除目标条目，避免残留脏数据
        list.Clear();
        _targetDict.Remove(target);
    }

    // ==================== 触发（立即派发） ====================

    /// <summary>触发无参数事件：立刻同步调用所有订阅者（依赖调用顺序的场景用这个）</summary>
    public void Dispatch(E_EventType evt)
    {
        if (_eventDict.TryGetValue(evt, out var info))
        {
            if (info is EventContainer eventInfo)
                eventInfo.Invoke();
            else
                Debug.LogError($"[EventCenter] 触发事件 {evt} 时类型不匹配，" +
                               $"期望无参，实际存储类型 {info.GetType().Name}");
        }
        // 事件不存在（没人订阅过）→ 静默跳过，这是合法场景
    }

    /// <summary>触发带参数事件：立刻同步调用所有订阅者，并把 data 传给回调</summary>
    public void Dispatch<T>(E_EventType evt, T data)
    {
        if (_eventDict.TryGetValue(evt, out var info))
        {
            if (info is EventContainer<T> eventInfo)
                eventInfo.Invoke(data);
            else
                Debug.LogError($"[EventCenter] 触发事件 {evt} 时类型不匹配，" +
                               $"期望 {typeof(T).Name}，实际存储类型 {GetEventArgType(info)}");
        }
    }

    // ==================== 清理 ====================

    /// <summary>移除指定事件的所有监听（单次清理：重开某关卡重置它的状态时用）</summary>
    public void ClearEvent(E_EventType evt)
    {
        _eventDict.Remove(evt);
    }

    /// <summary>清空所有事件与订阅记录（场景切换/游戏结束调用）</summary>
    public void ClearAll()
    {
        _eventDict.Clear();
        _targetDict.Clear();
    }

    // ==================== 内部辅助 ====================

    /// <summary>记录订阅目标（target 为 null 时不记录，仅防泄漏用）</summary>
    /// <remarks>
    /// 在 AddListener 里同步调用，把 (事件, 回调) 挂到 target 名下，
    /// 供 UnregisterTarget 反向查找。
    /// </remarks>
    private void Track(object target, E_EventType evt, Delegate callback)
    {
        if (target == null) return;

        if (!_targetDict.TryGetValue(target, out var list))
        {
            list = new List<(E_EventType evt, Delegate callback)>();
            _targetDict[target] = list;
        }
        list.Add((evt, callback));
    }

    /// <summary>移除一条订阅记录（RemoveListener 时同步调用，与 Track 对称）</summary>
    private void Untrack(object target, E_EventType evt, Delegate callback)
    {
        if (target == null || !_targetDict.TryGetValue(target, out var list)) return;

        // 倒序遍历删除，避免正序删除导致索引错位
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].evt == evt && list[i].callback == callback)
            {
                list.RemoveAt(i);
                break;
            }
        }
        // 该对象名下没有订阅记录了，直接移除条目保持字典干净
        if (list.Count == 0) _targetDict.Remove(target);
    }

    /// <summary>提取带参容器的泛型参数类型名（错误提示用）</summary>
    private static string GetEventArgType(EventContainerBase info)
    {
        var t = info.GetType();
        var args = t.GetGenericArguments();
        return args.Length > 0 ? args[0].Name : t.Name;
    }
}