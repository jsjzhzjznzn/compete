using System.Collections;
using UnityEngine.UI;
using UnityEngine;

namespace SkierFramework
{
    /// <summary>
    /// 格斗式 HUD 血条面板（左=我方，右=敌方）。
    /// 数据源：双方角色身上的 HealthModel（BindableProperty 直订刷新填充条），
    /// 头像按角色身上的 Player.NetCharId 加载（服务端生成时写入，随生成同步到所有端）。
    /// 打开时机：Player.OnNetworkSpawn（IsOwner 端）；单机不打开。
    /// </summary>
    public class bloodpanel : UIView
    {
        /// <summary>头像资源（CharId：安比=0，艾莲=1；YooAsset 收集器 Assets/Resource/ui/场景）</summary>
        private const string PortraitAnBi = "Assets/Resource/ui/场景/Snipaste_2026-09-10_18-25-50.png";
        private const string PortraitShaYu = "Assets/Resource/ui/场景/Snipaste_2026-09-10_18-25-22.png";

        /// <summary>对手可能晚于自己 spawn，绑定轮询间隔</summary>
        private const float BindPollInterval = 0.2f;

        #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		private Image bloodzhu;
		[ControlBinding]
		private Image renwu2;
		[ControlBinding]
		private Image renwu1;
		[ControlBinding]
		private Image bloodenemy;

		#pragma warning restore 0649
#endregion

        private Image _myFill;
        private Image _enemyFill;
        private HealthModel _myHealth;
        private HealthModel _enemyHealth;
        private Coroutine _bindCoroutine;

        public override void OnInit(UIControlData uIControlData, UIViewController controller)
        {
            base.OnInit(uIControlData, controller);

            _myFill = PrepareFillImage(bloodzhu, "zhublood");
            _enemyFill = PrepareFillImage(bloodenemy, "enemyblood");
        }

        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // 面板实例常驻 UIRoot（DDOL），跨局重开时先清掉上一局的绑定
            UnbindAll();
            _bindCoroutine = StartCoroutine(BindPlayers());
        }

        public override void OnClose()
        {
            base.OnClose();
            UnbindAll();
        }

        // ---------------- 绑定 ----------------

        /// <summary>
        /// 轮询找到我方（IsOwner）与敌方（非拥有者的已生成角色）后绑定血量与头像。
        /// 双方都绑定成功后协程结束。
        /// </summary>
        private IEnumerator BindPlayers()
        {
            Player mine = null;
            Player enemy = null;
            while (mine == null || enemy == null)
            {
                foreach (var player in FindObjectsByType<Player>())
                {
                    if (player.IsOwner) mine = player;
                    else if (player.IsSpawned) enemy = player;
                }
                if (mine == null || enemy == null)
                {
                    yield return new WaitForSeconds(BindPollInterval);
                }
            }

            BindSide(mine, ref _myHealth, _myFill, renwu1);
            BindSide(enemy, ref _enemyHealth, _enemyFill, renwu2);
            _bindCoroutine = null;
        }

        private void BindSide(Player player, ref HealthModel bound, Image fill, Image portrait)
        {
            var health = player.GetComponent<HealthModel>();
            if (health == null)
            {
                Debug.LogError($"[bloodpanel] 角色 {player.name} 上没有 HealthModel，该侧血条无法绑定！");
                return;
            }

            bound = health;
            health.CurrentHP.OnValueChanged += OnHPChanged;
            health.MaxHP.OnValueChanged += OnHPChanged;
            RefreshFill(fill, health);
            LoadPortrait(portrait, player.NetCharId.Value);
        }

        private void UnbindAll()
        {
            if (_bindCoroutine != null)
            {
                StopCoroutine(_bindCoroutine);
                _bindCoroutine = null;
            }
            UnbindSide(ref _myHealth);
            UnbindSide(ref _enemyHealth);
        }

        private void UnbindSide(ref HealthModel bound)
        {
            if (bound == null) return;
            bound.CurrentHP.OnValueChanged -= OnHPChanged;
            bound.MaxHP.OnValueChanged -= OnHPChanged;
            bound = null;
        }

        /// <summary>任一侧血量变化都全量刷新（两条填充条各刷一次，开销可忽略）</summary>
        private void OnHPChanged(float oldVal, float newVal)
        {
            RefreshFill(_myFill, _myHealth);
            RefreshFill(_enemyFill, _enemyHealth);
        }

        private static void RefreshFill(Image fill, HealthModel health)
        {
            if (fill == null || health == null) return;
            fill.fillAmount = health.MaxHP.Value > 0f
                ? Mathf.Clamp01(health.CurrentHP.Value / health.MaxHP.Value)
                : 0f;
        }

        /// <summary>取血条底框下的填充条子 Image，并强制为水平 Filled 模式</summary>
        private static Image PrepareFillImage(Image barBg, string childName)
        {
            if (barBg == null) return null;
            var fill = barBg.transform.Find(childName) != null
                ? barBg.transform.Find(childName).GetComponent<Image>()
                : null;
            if (fill == null)
            {
                Debug.LogError($"[bloodpanel] 血条底框 {barBg.name} 下找不到填充子节点 {childName}");
                return null;
            }
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            return fill;
        }

        private static void LoadPortrait(Image portrait, int charId)
        {
            if (portrait == null) return;
            string path;
            switch (charId)
            {
                case 0: path = PortraitAnBi; break;
                case 1: path = PortraitShaYu; break;
                default:
                    Debug.LogError($"[bloodpanel] 未知 CharId={charId}，头像不加载");
                    return;
            }
            ResourceManager.Instance.LoadAssetAsync<Sprite>(path, sprite =>
            {
                if (sprite != null) portrait.sprite = sprite;
            });
        }
    }
}
