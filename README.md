# Compete

> 基于 Unity 6 + URP 的 3D 动作格斗实时对战 Demo —— 以**双状态机**为骨架、**数据驱动**为血肉、**类型安全事件总线**为神经的动作游戏工程实践。

## 项目简介

Compete 是一个主打 **1v1 实时对战**的 3D 动作格斗原型，聚焦动作游戏的**手感工程（Game Feel）**与**多端一致性**。

项目从零搭建了完整战斗闭环：输入采样 → 状态机调度 → 命中判定 → 伤害管线 → 网络同步 → 表现反馈（血条 / 飘字 / 顿帧 / 震屏 / 空间音效），并在每个环节都做了**架构抽象**而非硬编码堆叠——新增招式、Buff、音效、特效均无需改动既有系统。

## 核心架构

### 双状态机驱动（Dual FSM Architecture）

- **移动状态机 + 连击状态机**并行调度，基于泛型状态机基类与**状态对象化（State Pattern）**：每个状态一个类，生命周期钩子（Enter / Exit / Update）统一由基类契约约束，新增状态零侵入
- 连招段推进采用**输入缓冲（Input Buffering）**：`linkCancelTime` 归一化时间窗内按键只写指令不立即出手，到 `Link Checkpoint` 统一消费，避免"按了没反应"的判定丢失
- 基类统一处理"读输入 → 写共享数据（ReusableData）→ 平滑转向"，子类只关心状态切换逻辑，**模板方法模式**约束状态实现

### 数据驱动配置管线（Data-Driven Pipeline）

- **ScriptableObject + Luban** 双层配置体系：连招段、移动参数、音效、VFX 全部资产化，策划可独立调参
- 每段攻击独立一份 `ComboData` 资产，统一管理动画、收尾动画、伤害、暴击率 / 倍率、命中 Buff、判定半径、帧暂停等十余项参数
- 招式类型枚举化（Attack / Skill / FinishSkill / SwitchSkill），连招容器支持**运行时切换变体**（如前进攻击段替换首段），一套配置多套行为

### 多端状态同步（Multi-Client State Sync）

- 基于 **Unity Netcode for GameObjects**，采用**拥有者权威（Owner-Authoritative）**结算模型：血量、Buff、无敌状态仅由拥有者端模拟，杜绝双端重复结算
- 状态转移通过 `NetworkVariable` 广播，配合 **ServerTime 时间戳相位对齐（Phase Offset Alignment）**：远程端按"已播放时长"定位动画起播点，而非从头重放，解决网络抖动下的错位与超前
- 伤害链路 **ServerRpc → ClientRpc 定向转发**，仅目标拥有者端执行本地结算（受击 / 飘字 / 硬直 / 血条全走本地链路）；血量变化实时同步到全员，远程端血条零改动刷新

### 类型安全事件总线（Type-Safe Event Bus）

- 基于**枚举事件名 + 泛型容器 + struct 参数包裹**的发布 / 订阅总线：事件签名在**编译期**锁定，杜绝字符串魔法值与运行期参数错配
- 战斗、Buff、UI、音效、镜头系统间零直接引用，**关注点分离（Separation of Concerns）**，模块可独立演进与复用

### 战斗手感工程（Game Feel）

- 命中**顿帧（Hit-Stop）**：时间缩放制造打击停顿，实时协程计时保证恢复；Buff 计时与 `timeScale` 解耦对齐，慢动作下表现自洽
- **无敌帧（I-Frames）**闪避窗口：窗口内受击拦截伤害并触发闪避动画，DoT 无视无敌——"闪避躲单发、躲不掉持续伤害"的规则清晰可配
- **伤害修饰管线（Damage Pipeline）**：基础伤害 × 暴击骰 → Buff 增伤 / 减伤系数 → 命中附带 Buff，`DamageCalculator` 单点收口
- **Buff 策略模式（Strategy Pattern）**：效果逻辑委托给 `BuffEffect` 子类，支持叠加层数、周期 tick（灼烧 / 回血）、生命周期钩子（Apply / Tick / Expire / Remove），扩展新效果无需改调度器（**开闭原则**）

### 表现层

- **MVVM 架构血条**：纯 C# ViewModel（无 MonoBehaviour 依赖）负责数据投影，`BindableProperty` 数据绑定驱动 View，逻辑层可单元测试
- **VFX 对象池化（Object Pooling）**：特效与飘字运行时复用，避免高频战斗场景下的 GC 压力与实例化抖动
- **Cinemachine 程序化运镜** + 相机回正；**AudioMixer 音频总线** + 3D 空间化音效，打击感多维反馈

## 技术栈

| 类别 | 技术 |
| ---- | ---- |
| 引擎 | Unity 6000.4.0f1 (Unity 6) |
| 渲染 | URP (Universal Render Pipeline) |
| 网络 | Unity Netcode for GameObjects + UOS KCP 传输 |
| 动画 | Animancer（动态动画重定向 / 相位对齐） |
| 输入 | Input System（键鼠 + 移动端触控） |
| 配置 | Luban + ScriptableObject 双管线 |
| 镜头 | Cinemachine |
| 其他 | DOTween、MagicaCloth V2、Behavior Designer |

## 项目结构

```
Assets/
├── script/          # 游戏脚本
│   ├── 状态机/       # 泛型状态机基类 + 移动/连击双状态机
│   ├── player/      # 角色主控、角色数据资产（PlayerSO）、网络同步入口
│   ├── damage/      # 伤害管线：DamageCalculator / Context / Result
│   ├── buff/        # 策略模式 Buff 系统（叠加 / tick / 生命周期）
│   ├── ui/          # MVVM 血条、飘字对象池
│   ├── vfx/         # 特效管理器 + 对象池
│   ├── sound/       # 音频总线、3D 空间音效、角色音效组件
│   ├── 镜头/         # Cinemachine 运镜辅助
│   ├── network/     # 网络管理器 UI、玩家生成器、选人
│   ├── 事件/         # 类型安全事件总线（EventCenter）
│   ├── 单例/ 基类/    # 泛型单例 / 通用基类
│   └── tool/        # 定时器系统（GameTimer / TimerManager）
├── Scenes/          # 场景（SampleScene）
├── Resource/        # 音频资源
├── Screenshots/     # 开发截图
└── plugins/         # 第三方插件（Animancer 等）
```

## 截图

![柯琳角色展示](Assets/Screenshots/screenshot-20260815-172039.png)

## 快速开始

1. 安装 [Unity 6000.4.0f1](https://unity.com/releases/editor/whats-new/6000.4.0)
2. 用 Unity Hub 打开本仓库目录
3. 打开 `Assets/Scenes/SampleScene.unity`
4. 点击 Play 运行

## 依赖

- 需要联网拉取 UPM 包：UOS KCP 传输、Luban、unity-mcp 等（见 `Packages/manifest.json`）
- 第三方插件 Animancer 已随仓库包含在 `Assets/plugins/` 下
