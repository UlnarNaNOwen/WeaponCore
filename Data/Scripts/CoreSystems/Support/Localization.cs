﻿using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage;

namespace CoreSystems.Support
{
    public static class Localization
    {
        private static readonly Dictionary<MyLanguagesEnum, Dictionary<string, string>> I18NDictionaries =
            new Dictionary<MyLanguagesEnum, Dictionary<string, string>>
            {
                {
                    MyLanguagesEnum.English, new Dictionary<string, string>
                    {
                        { "ReportTarget Toggle On/Off", "ReportTarget Toggle On/Off" },
                        { "ReportTarget On", "ReportTarget On" },
                        { "ReportTarget Off", "ReportTarget Off" },
                        { "Increase Weapon Damage", "Increase Weapon Damage" },
                        { "Decrease Weapon Damage", "Decrease Weapon Damage" },
                        { "Increase Weapon ROF", "Increase Weapon ROF" },
                        { "Decrease Weapon ROF", "Decrease Weapon ROF" },
                        { "Overload Toggle On/Off", "Overload Toggle On/Off" },
                        { "Overload On", "Overload On" },
                        { "Overload Off", "Overload Off" },
                        { "TerminalSwitchOn", "On" },
                        { "TerminalSwitchOff", "Off" },
                        { "TerminalReportTargetTitle", "Enable Report Target" },
                        { "TerminalReportTargetTooltip", "Report if weapon has target on HUD" },
                        { "TerminalWeaponROFTitle", "Change Rate of Fire" },
                        { "TerminalWeaponROFTooltip", "Change rate of fire" },
                        { "TerminalOverloadTitle", "Overload Damage" },
                        { "TerminalOverloadTooltip", "Overload damage" },
                        { "TerminalDetonationTitle", "Detonation time" },
                        { "TerminalDetonationTooltip", "Detonation time" },
                        { "TerminalGravityTitle", "Gravity Offset" },
                        { "TerminalGravityTooltip", "Adjust gravity influence factor, lower values decrease range, higher values increase it" },
                        { "TerminalStartCountTitle", "Start Countdown" },
                        { "TerminalStartCountTooltip", "Start Countdown" },
                        { "TerminalStopCountTitle", "Stop Countdown" },
                        { "TerminalStopCountTooltip", "Stop Countdown" },
                        { "TerminalArmTitle", "Arm Reaction" },
                        { "TerminalArmTooltip", "When checked, the warhead can be detonated manually or by unwary handle." },
                        { "TerminalTriggerTitle", "Trigger" },
                        { "TerminalTriggerTooltip", "Trigger" },
                        { "TerminalWeaponRangeTitle", "Aiming Radius" },
                        { "TerminalWeaponRangeTooltip", "Change the min/max targeting range" },
                        { "TerminalNeutralsTitle", "Target Neutrals" },
                        { "TerminalNeutralsTooltip", "Fire on targets that are neutral" },
                        { "TerminalUnownedTitle", "Target Unowned" },
                        { "TerminalUnownedTooltip", "Fire on targets with no owner" },
                        { "TerminalBiologicalsTitle", "Target Biologicals" },
                        { "TerminalBiologicalsTooltip", "Fire on players and biological NPCs" },
                        { "TerminalProjectilesTitle", "Target Projectiles" },
                        { "TerminalProjectilesTooltip", "Fire on incoming projectiles" },
                        { "TerminalMeteorsTitle", "Target Meteors" },
                        { "TerminalMeteorsTooltip", "Target Meteors" },
                        { "TerminalGridsTitle", "Target Grids" },
                        { "TerminalGridsTooltip", "Target Grids" },
                        { "TerminalFocusFireTitle", "Target FocusFire" },
                        { "TerminalFocusFireTooltip", "Focus all fire on the specified target" },
                        { "TerminalSubSystemsTitle", "Target SubSystems" },
                        { "TerminalSubSystemsTooltip", "Target specific SubSystems of a target" },
                        { "TerminalRepelTitle", "Repel Mode" },
                        { "TerminalRepelTooltip", "Aggressively focus and repel small threats" },
                        { "TerminalPickAmmoTitle", "Pick Ammo" },
                        { "TerminalPickAmmoTooltip", "Select the ammo type to use" },
                        { "TerminalPickSubSystemTitle", "Pick SubSystem" },
                        { "TerminalPickSubSystemTooltip", "Select the target subsystem to focus fire on" },
                        { "TerminalTrackingModeTitle", "Tracking Mode" },
                        { "TerminalTrackingModeTooltip", "Movement fire control requirements" },
                        { "TerminalControlModesTitle", "Control Mode" },
                        { "TerminalControlModesTooltip", "Select the aim control mode for the weapon" },
                        { "TerminalCameraChannelTitle", "Weapon Camera Channel" },
                        { "TerminalCameraChannelTooltip", "Assign this weapon to a camera channel" },
                        { "TerminalBurstShotsTitle", "Burst Shot Count" },
                        { "TerminalBurstShotsTooltip", "The number of shots to burst at a time" },

                        { "TerminalBurstDelayTitle", "Shot Delay" },
                        { "TerminalBurstDelayTooltip", "The number game ticks (60 per second) to delay between shots" },


                        { "TerminalSequenceIdTitle", "Weapon Sequence id" },
                        { "TerminalSequenceIdTooltip", "Assign this weapon a unique sequence id per weapon group, used for sequence firing" },

                        { "TerminalWeaponGroupIdTitle", "Weapon Group id" },
                        { "TerminalWeaponGroupIdTooltip", "Assign this weapon to a sequence group, used for sequence firing" },

                        { "TerminalShootModeTitle", "Shoot Mode" },
                        { "TerminalShootModeTooltip", "Set the weapon's mode, fire once, burst fire, mouse click, key toggle mode" },

                        { "TerminalAiEnabledTitle", "Enable AI" },
                        { "TerminalAiEnabledTooltip", "Automatically aim and fire at targets" },

                        { "TerminalShareFireControlTitle", "Share Control" },
                        { "TerminalShareFireControlTooltip", "Weapons with manual/painter/mousecontrol enabled will respond when using the Control button above" },

                        { "TerminalTargetGroupTitle", "Target Lead Group" },
                        { "TerminalTargetGroupTooltip", "Assign this weapon to target lead group" },
                        { "TerminalDecoyPickSubSystemTitle", "Pick SubSystem" },
                        { "TerminalDecoyPickSubSystemTooltip", "Pick what subsystem this decoy will imitate" },
                        { "TerminalCameraCameraChannelTitle", "Camera Channel" },
                        { "TerminalCameraCameraChannelTooltip", "Assign the camera weapon channel to this camera" },
                        { "TerminalDebugTitle", "Debug" },

                        { "TerminalDebugTooltip", "Debug On/Off" },
                        { "TerminalAdvancedTitle", "Advanced Features" },
                        { "TerminalAdvancedTooltip", "This enables more advanced UI features that tend to be confusing to new users" },
                        { "TerminalShootTitle", "Shoot" },
                        { "TerminalShootTooltip", "Shoot On/Off Toggle, this will be retired, replaced by Shoot Mode KeyToggle" },
                        { "ActionStateOn", "On" },
                        { "ActionStateOff", "Off" },
                        { "ActionWCShootMode", "Cycle Shoot Modes" },
                        { "ActionWCMouseToggle", "Toggle Mouse Shoot (Mode: MouseControl)" },
                        { "ActionFire", "Shoot (Modes: KeyToggle/KeyFire)" },
                        { "ForceReload", "Force Reload" },
                        { "ActionShoot", "Shoot On/Off" },
                        { "ActionShoot_Off", "Shoot Off" },
                        { "ActionShoot_On", "Shoot On" },
                        { "ActionSubSystems", "Cycle SubSystems" },
                        { "ActionNextCameraChannel", "Next Channel" },
                        { "ActionPreviousCameraChannel", "Previous Channel" },
                        { "ActionControlModes", "Control Mode" },
                        { "ActionNeutrals", "Neutrals On/Off" },
                        { "ActionProjectiles", "Projectiles On/Off" },
                        { "ActionBiologicals", "Biologicals On/Off" },
                        { "ActionMeteors", "Meteors On/Off" },
                        { "ActionGrids", "Grids On/Off" },
                        { "ActionFriendly", "Friendly On/Off" },
                        { "ActionUnowned", "Unowned On/Off" },
                        { "ActionFocusTargets", "FocusTargets On/Off" },
                        { "ActionFocusSubSystem", "FocusSubSystem On/Off" },
                        { "ActionMaxSizeIncrease", "MaxSize Increase" },
                        { "ActionMaxSizeDecrease", "MaxSize Decrease" },
                        { "ActionMinSizeIncrease", "MinSize Increase" },
                        { "ActionMinSizeDecrease", "MinSize Decrease" },
                        { "ActionTrackingMode", "Tracking Mode" },
                        { "ActionWC_CycleAmmo", "Cycle Consumable" },
                        { "ActionWC_RepelMode", "Repel Mode" },
                        { "ActionWC_Increase_CameraChannel", "Next Camera Channel" },
                        { "ActionWC_Decrease_CameraChannel", "Previous Camera Channel" },
                        { "ActionWC_Increase_LeadGroup", "Next Lead Group" },
                        { "ActionWC_Decrease_LeadGroup", "Previous Lead Group" },

                        { "ActionWCAiEnabled", "Enable AI On/Off" },
                        { "ActionShareFireControl", "Share Control On/Off" },

                        { "ActionMask", "Select Mask Type" },
                        { "SystemStatusFault", "[Fault]" },
                        { "SystemStatusOffline", "[Offline]" },
                        { "SystemStatusOnline", "[Online]" },
                        { "SystemStatusRogueAi", "[Rogue Ai] Parts are unowned!!" },
                        { "WeaponInfoConstructDPS", "Construct DPS" },
                        { "WeaponInfoShotsPerSec", "ShotsPerSec" },
                        { "WeaponInfoRealDps", "RealDps" },
                        { "WeaponInfoPerfectDps", "PerfectDps" },
                        { "WeaponInfoPeakDps", "PeakDps" },
                        { "WeaponInfoBaseDps", "BaseDps" },
                        { "WeaponInfoAreaDps", "AreaDps" },
                        { "WeaponInfoExplode", "Explode" },
                        { "WeaponInfoCurrent", "Current" },
                        { "WeaponInfoHeatGenerated", "Heat Generated" },
                        { "WeaponInfoHeatDissipated", "Heat Dissipated" },
                        { "WeaponInfoCurrentHeat", "Current Heat" },
                        { "WeaponInfoCurrentDraw", "Current Draw" },
                        { "WeaponInfoRequiredPower", "Power Per Shot" },
                        { "WeaponInfoDividerLineWeapon", "==== Weapons ====" },
                        { "WeaponInfoName", "Name" },
                        { "WeaponInfoBurst", "Shoot (for keyboard ShootModes)" },
                        { "WeaponInfoDelay", "Delay" },
                        { "WeaponInfoReloading", "Reloading" },
                        { "WeaponInfoLoS", "LoS" },
                        { "WeaponTotalEffect", "Damage" },
                        { "WeaponTotalEffectAvgDps", "AvgDps" },
                        { "TerminalOverrideTitle", "Override" },
                        { "TerminalOverrideTooltip", "Allow dumb firing weapons that otherwise require a target, for target practice only" },
                        { "WeaponInfoHasTarget", "HasTarget" },

                    }
                },
               {
                    MyLanguagesEnum.Russian, new Dictionary<string, string>
                    {
                        { "ReportTarget Toggle On/Off", "Переключить Наведение Вкл/Выкл" },
                        { "ReportTarget On", "Переключить Наведение Вкл" },
                        { "ReportTarget Off", "Переключить Наведение Выкл" },
                        { "Increase Weapon Damage", "Увеличить Наносимый урон" },
                        { "Decrease Weapon Damage", "Уменьшить Наносимый урон" },
                        { "Increase Weapon ROF", "Увеличить Скорострельность" },
                        { "Decrease Weapon ROF", "Уменьшить Скорострельность" },
                        { "Overload Toggle On/Off", "Переключить Перегрузку Вкл/Выкл " },
                        { "Overload On", "Переключить Перегрузку Вкл" },
                        { "Overload Off", "Переключить Перегрузку Выкл" },
                        { "TerminalSwitchOn", "Вкл" },
                        { "TerminalSwitchOff", "Выкл" },
                        { "TerminalReportTargetTitle", "Включить Наведение" },
                        { "TerminalReportTargetTooltip", "Включить наводку турелей" },
                        { "TerminalWeaponROFTitle", "Изменить скорострельность" },
                        { "TerminalWeaponROFTooltip", "Изменить скорострельность" },
                        { "TerminalOverloadTitle", "Получаемый урон от перегрузки" },
                        { "TerminalOverloadTooltip", "Получаемый урон от перегрузки" },
                        { "TerminalDetonationTitle", "Время детонации" },
                        { "TerminalDetonationTooltip", "Время детонации" },
                        { "TerminalGravityTitle", "Коррекция гравитации" },
                        { "TerminalGravityTooltip", "Фактор коррекции гравитации, более низкие значения уменьшают дальность, более высокие значения увеличивают её" },
                        { "TerminalStartCountTitle", "Запустить отсчет" },
                        { "TerminalStartCountTooltip", "Запустить отсчет" },
                        { "TerminalStopCountTitle", "Остановить отсчет" },
                        { "TerminalStopCountTooltip", "Остановить отсчет" },
                        { "TerminalArmTitle", "Предохранитель" },
                        { "TerminalArmTooltip", "Если включено, боеголовка может быть взорвана вручную." },
                        { "TerminalTriggerTitle", "Подорвать" },
                        { "TerminalTriggerTooltip", "Подорвать" },
                        { "TerminalWeaponRangeTitle", "Радиус прицеливания" },
                        { "TerminalWeaponRangeTooltip", "Радиус прицеливания" },
                        { "TerminalNeutralsTitle", "Наводить на нейтральных" },
                        { "TerminalNeutralsTooltip", "Стрелять по нейтральным" },
                        { "TerminalUnownedTitle", "Наводить на цели без владельца" },
                        { "TerminalUnownedTooltip", "Стрелять по целям без владельца" },
                        { "TerminalBiologicalsTitle", "Наводить на живую силу" },
                        { "TerminalBiologicalsTooltip", "Огонь по игрокам и NPC" },
                        { "TerminalProjectilesTitle", "Наводить на снаряды" },
                        { "TerminalProjectilesTooltip", "Огонь по приближающимся снарядам" },
                        { "TerminalMeteorsTitle", "Наводить на метеориты" },
                        { "TerminalMeteorsTooltip", "Огонь по приближающимся метеоритам" },
                        { "TerminalGridsTitle", "Наводить на большие/малые блоки" },
                        { "TerminalGridsTooltip", "Огонь по большим/малым блокам" },
                        { "TerminalFocusFireTitle", "Фокусировать огонь" },
                        { "TerminalFocusFireTooltip", "Сосредоточивать весь огонь на указанной цели" },
                        { "TerminalSubSystemsTitle", "Наводить на подсистемы" },
                        { "TerminalSubSystemsTooltip", "Огонь по подсистемам цели" },
                        { "TerminalRepelTitle", "Режим Подавления" },
                        { "TerminalRepelTooltip", "Сосредоточить огонь на приближающихся малых объектах" },
                        { "TerminalPickAmmoTitle", "Вид боеприпасов" },
                        { "TerminalPickAmmoTooltip", "Выберите тип боеприпасов для использования" },
                        { "TerminalPickSubSystemTitle", "Подсистема для наведения" },
                        { "TerminalPickSubSystemTooltip", "Выберите целевую подсистему, на которой нужно сосредоточить огонь" },
                        { "TerminalTrackingModeTitle", "Отслеживать цели" },
                        { "TerminalTrackingModeTooltip", "Стрелять только по Двигающимся/Всем, кроме неподвижных/Неподвижным" },
                        { "TerminalControlModesTitle", "Управление прицелом" },
                        { "TerminalControlModesTooltip", "Выберите режим управления прицелом для оружия." },
                        { "TerminalCameraChannelTitle", "Канал оружейной камеры" },
                        { "TerminalCameraChannelTooltip", "На камере с таким же каналом, будет проецироваться прицел с опережением" },
                        { "TerminalBurstShotsTitle", "Размер очереди" },
                        { "TerminalBurstShotsTooltip", "Кол-во выстрелов в 1 очереди" },
                        { "TerminalTargetGroupTitle", "Ведущая оружейная группа" },
                        { "TerminalTargetGroupTooltip", "Установите идентичные цифры на камере м орудиях для проекции прицела" },
                        { "TerminalDecoyPickSubSystemTitle", "Имитация подсистемы" },
                        { "TerminalDecoyPickSubSystemTooltip", "Выберите, какую подсистему будет имитировать эта приманка" },
                        { "TerminalCameraCameraChannelTitle", "Канал камеры" },
                        { "TerminalCameraCameraChannelTooltip", "Установите индентичные цифры на ведущей группе орудий для проекции прицела" },
                        { "TerminalDebugTitle", "Отладка" },
                        { "TerminalDebugTooltip", "Отладка Вкл/Выкл" },
                        { "TerminalShootTitle", "Стрелять" },
                        { "TerminalShootTooltip", "Стрелять Вкл/Выкл" },
                        { "ActionStateOn", "Вкл" },
                        { "ActionStateOff", "Выкл" },
                        { "ActionWCShootMode", "Огонь по нажатию" },
                        { "ActionFire", "Выстрелить один раз" },
                        { "ActionShoot", "Стрелять Вкл/Выкл" },
                        { "ActionShoot_Off", "Стрелять Выкл" },
                        { "ActionShoot_On", "Стрелять Вкл" },
                        { "ActionSubSystems", "Выбрать Подсистему" },
                        { "ActionNextCameraChannel", "Следующий канал" },
                        { "ActionPreviousCameraChannel", "Предыдущий оружейный канал" },
                        { "ActionControlModes", "Управление прицелом" },
                        { "ActionNeutrals", "Наводить на Нейтральных Вкл/Выкл" },
                        { "ActionProjectiles", "Наводить на Снаряды Вкл/Выкл" },
                        { "ActionBiologicals", "Наводить на Живую силу Вкл/Выкл" },
                        { "ActionMeteors", "Наводить на Метеориты Вкл/Выкл" },
                        { "ActionGrids", "Наводить на Большие/малые блоки Вкл/Выкл" },
                        { "ActionFriendly", "Наводить на Союзников Вкл/Выкл" },
                        { "ActionUnowned", "Наводить на Цели без владельца Вкл/Выкл" },
                        { "ActionFocusTargets", "Наводить на Цель фокуса Вкл/Выкл" },
                        { "ActionFocusSubSystem", "Наводить на Подсистемы Вкл/Выкл" },
                        { "ActionMaxSizeIncrease", "Увеличить Макс. размер захватываемой цели" },
                        { "ActionMaxSizeDecrease", "Уменьшить Макс. размер захватываемой цели" },
                        { "ActionMinSizeIncrease", "Увеличение Мин. размер захватываемой цели" },
                        { "ActionMinSizeDecrease", "Уменьшение Мин. размер захватываемой цели" },
                        { "ActionTrackingMode", "Отслеживание целей" },
                        { "ActionWC_CycleAmmo", "Изменить тип боеприпаса" },
                        { "ActionWC_RepelMode", "Режим Подавления" },
                        { "ActionWC_Increase_CameraChannel", "Следующий канал оружейной камеры" },
                        { "ActionWC_Decrease_CameraChannel", "Предыдущий канал оружейной камеры" },
                        { "ActionWC_Increase_LeadGroup", "Следующая ведущая оружейная группа" },
                        { "ActionWC_Decrease_LeadGroup", "Предыдущая ведущая оружейная группа" },
                        { "ActionMask", "Имитировать подсистему" },
                        { "SystemStatusFault", "[Повреждено]" },
                        { "SystemStatusOffline", "[Выкл]" },
                        { "SystemStatusOnline", "[Вкл]" },
                        { "SystemStatusRogueAi", "[Без владельца]" },
                        { "WeaponInfoConstructDPS", "Общий УРОН В СЕКУНДУ" },
                        { "WeaponInfoShotsPerSec", "Выстрелов в секунду" },
                        { "WeaponInfoRealDps", "Стандартный ДПС" },
                        { "WeaponInfoPerfectDps", "Идеальный ДПС" },
                        { "WeaponInfoPeakDps", "Максимальный ДПС" },
                        { "WeaponInfoBaseDps", "Базовый ДПС" },
                        { "WeaponInfoAreaDps", "По площади ДПС" },
                        { "WeaponInfoExplode", "Взрывной ДПС" },
                        { "WeaponInfoCurrent", "Текущий" },
                        { "WeaponInfoHeatGenerated", "Нагрев" },
                        { "WeaponInfoHeatDissipated", "Охлаждение" },
                        { "WeaponInfoCurrentHeat", "Текущий нагрев" },
                        { "WeaponInfoCurrentDraw", "Макс. потребление" },
                        { "WeaponInfoRequiredPower", "Требуемое потребление" },
                        { "WeaponInfoDividerLineWeapon", "==== Оружие ====" },
                        { "WeaponInfoName", "Название" },
                        { "WeaponInfoBurst", "Очередь" },
                        { "WeaponInfoDelay", "Задержка" },
                        { "WeaponInfoReloading", "Перезарядка" },
                        { "WeaponInfoLoS", "Прямая видимость" },
                        { "WeaponTotalEffect", "Повреждать" },
                        { "WeaponTotalEffectAvgDps", "Средний урон в секунду" },
                        { "TerminalOverrideTitle", "Переопределие" },
                        { "TerminalOverrideTooltip", "Разрешить стрельбу орудиям, иначе требующие цель, только для тестирования цели" },
                        { "WeaponInfoHasTarget", "Имеет цель" },
                    }
                },
                {
                    MyLanguagesEnum.ChineseChina, new Dictionary<string, string>
                    {
                        { "ReportTarget Toggle On/Off", "切换制导 开启/关闭" },
                        { "ReportTarget On", "制导 开启" },
                        { "ReportTarget Off", "制导 关闭" },
                        { "Increase Weapon Damage", "增加 武器伤害" },
                        { "Decrease Weapon Damage", "减少 武器伤害" },
                        { "Increase Weapon ROF", "增加 武器射速" },
                        { "Decrease Weapon ROF", "减少 武器射速" },
                        { "Overload Toggle On/Off", "切换过载 开启/关闭" },
                        { "Overload On", "过载 开启" },
                        { "Overload Off", "过载 关闭" },
                        { "TerminalSwitchOn", "开启" },
                        { "TerminalSwitchOff", "关闭" },
                        { "TerminalReportTargetTitle", "启用制导" },
                        { "TerminalReportTargetTooltip", "启用制导" },
                        { "TerminalWeaponROFTitle", "改变射速" },
                        { "TerminalWeaponROFTooltip", "改变射速" },
                        { "TerminalOverloadTitle", "过载伤害" },
                        { "TerminalOverloadTooltip", "过载伤害" },
                        { "TerminalDetonationTitle", "引爆时间" },
                        { "TerminalDetonationTooltip", "引爆时间" },
                        { "TerminalGravityTitle", "Gravity Offset" },
                        { "TerminalGravityTooltip", "Adjust gravity influence factor, lower values decrease range, higher values increase it" },
                        { "TerminalStartCountTitle", "开始倒计时" },
                        { "TerminalStartCountTooltip", "开始倒计时" },
                        { "TerminalStopCountTitle", "停止倒计时" },
                        { "TerminalStopCountTooltip", "停止倒计时" },
                        { "TerminalArmTitle", "装备" },
                        { "TerminalArmTooltip", "装备反应" },
                        { "TerminalTriggerTitle", "触发" },
                        { "TerminalTriggerTooltip", "触发" },
                        { "TerminalWeaponRangeTitle", "瞄准半径" },
                        { "TerminalWeaponRangeTooltip", "改变最小/最大瞄准范围" },
                        { "TerminalNeutralsTitle", "攻击中立方" },
                        { "TerminalNeutralsTooltip", "向中立目标开火" },
                        { "TerminalUnownedTitle", "攻击无主" },
                        { "TerminalUnownedTooltip", "向无主目标开火" },
                        { "TerminalBiologicalsTitle", "攻击角色" },
                        { "TerminalBiologicalsTooltip", "向玩家和生物性非玩家角色开火" },
                        { "TerminalProjectilesTitle", "攻击导弹" },
                        { "TerminalProjectilesTooltip", "向来袭的投射物开火" },
                        { "TerminalMeteorsTitle", "攻击流星" },
                        { "TerminalMeteorsTooltip", "向流星开火" },
                        { "TerminalGridsTitle", "攻击网格" },
                        { "TerminalGridsTooltip", "向网格开火" },
                        { "TerminalFocusFireTitle", "集火" },
                        { "TerminalFocusFireTooltip", "将所有火力集中在指定目标上" },
                        { "TerminalSubSystemsTitle", "瞄准子系统" },
                        { "TerminalSubSystemsTooltip", "瞄准目标上的特定子系统" },
                        { "TerminalRepelTitle", "击退模式" },
                        { "TerminalRepelTooltip", "着重击退小型威胁" },
                        { "TerminalPickAmmoTitle", "选择弹药" },
                        { "TerminalPickAmmoTooltip", "选择要使用的弹药类型" },
                        { "TerminalPickSubSystemTitle", "选择子系统" },
                        { "TerminalPickSubSystemTooltip", "选择要集火的子系统" },
                        { "TerminalTrackingModeTitle", "追踪模式" },
                        { "TerminalTrackingModeTooltip", "移动火控必选" },
                        { "TerminalControlModesTitle", "控制模式" },
                        { "TerminalControlModesTooltip", "选择武器的瞄准控制模式" },
                        { "TerminalCameraChannelTitle", "武器摄像头频道" },
                        { "TerminalCameraChannelTooltip", "将此武器分配到一个摄像头频道" },
                        { "TerminalBurstShotsTitle", "Burst Shot Count" },
                        { "TerminalBurstShotsTooltip", "The number of shots to burst at a time" },
                        { "TerminalTargetGroupTitle", "目标引导组" },
                        { "TerminalTargetGroupTooltip", "将此武器分配到目标引导组" },
                        { "TerminalDecoyPickSubSystemTitle", "选择子系统" },
                        { "TerminalDecoyPickSubSystemTooltip", "选择这个诱饵将模仿的子系统" },
                        { "TerminalCameraCameraChannelTitle", "摄像机频道" },
                        { "TerminalCameraCameraChannelTooltip", "将武器摄像头频道绑定到此摄像头上" },
                        { "TerminalDebugTitle", "调试" },
                        { "TerminalDebugTooltip", "调试 开启/关闭" },
                        { "TerminalShootTitle", "射击" },
                        { "TerminalShootTooltip", "射击 开启/关闭" },
                        { "ActionStateOn", "开启" },
                        { "ActionStateOff", "关闭" },
                        { "ActionWCShootMode", "切换点击开火" },
                        { "ActionFire", "射击一次" },
                        { "ActionShoot", "射击 开启/关闭" },
                        { "ActionShoot_Off", "射击 关闭" },
                        { "ActionShoot_On", "射击 开启" },
                        { "ActionSubSystems", "循环子系统" },
                        { "ActionNextCameraChannel", "下一个频道" },
                        { "ActionPreviousCameraChannel", "上一个频道" },
                        { "ActionControlModes", "控制模式" },
                        { "ActionNeutrals", "攻击中立方 开启/关闭" },
                        { "ActionProjectiles", "攻击导弹 开启/关闭" },
                        { "ActionBiologicals", "攻击角色 开启/关闭" },
                        { "ActionMeteors", "攻击流星 开启/关闭" },
                        { "ActionGrids", "攻击网格 开启/关闭" },
                        { "ActionFriendly", "攻击友方 开启/关闭" },
                        { "ActionUnowned", "攻击无主 开启/关闭" },
                        { "ActionFocusTargets", "集火 开启/关闭" },
                        { "ActionFocusSubSystem", "瞄准子系统 开启/关闭" },
                        { "ActionMaxSizeIncrease", "最大大小增加" },
                        { "ActionMaxSizeDecrease", "最大大小减少" },
                        { "ActionMinSizeIncrease", "最小大小增加" },
                        { "ActionMinSizeDecrease", "最小大小减少" },
                        { "ActionTrackingMode", "追踪模式" },
                        { "ActionWC_CycleAmmo", "循环消耗品" },
                        { "ActionWC_RepelMode", "击退模式" },
                        { "ActionWC_Increase_CameraChannel", "下一个摄像头频道" },
                        { "ActionWC_Decrease_CameraChannel", "上一个摄像头频道" },
                        { "ActionWC_Increase_LeadGroup", "下一个目标引导组" },
                        { "ActionWC_Decrease_LeadGroup", "上一个目标引导组" },
                        { "ActionMask", "选择伪装类型" },
                        { "SystemStatusFault", "[错误]" },
                        { "SystemStatusOffline", "[离线]" },
                        { "SystemStatusOnline", "[在线]" },
                        { "SystemStatusRogueAi", "[Rogue Ai] 未拥有此部件!!" },
                        { "WeaponInfoConstructDPS", "理论每秒伤害" },
                        { "WeaponInfoShotsPerSec", "每秒射击次数" },
                        { "WeaponInfoRealDps", "实际每秒伤害" },
                        { "WeaponInfoPerfectDps", "完美的伤害" },
                        { "WeaponInfoPeakDps", "峰值每秒伤害" },
                        { "WeaponInfoBaseDps", "基础每秒伤害" },
                        { "WeaponInfoAreaDps", "范围每秒伤害" },
                        { "WeaponInfoExplode", "爆炸伤害" },
                        { "WeaponInfoCurrent", "当前每秒伤害" },
                        { "WeaponInfoHeatGenerated", "产热" },
                        { "WeaponInfoHeatDissipated", "散热" },
                        { "WeaponInfoCurrentHeat", "当前热量" },
                        { "WeaponInfoCurrentDraw", "当前耗能" },
                        { "WeaponInfoRequiredPower", "所需功率" },
                        { "WeaponInfoDividerLineWeapon", "==== 武器 ====" },
                        { "WeaponInfoName", "名称" },
                        { "WeaponInfoBurst", "爆发" },
                        { "WeaponInfoDelay", "延迟" },
                        { "WeaponInfoReloading", "装填中" },
                        { "WeaponInfoLoS", "视线" },
                        { "WeaponTotalEffect", "损害" },
                        { "WeaponTotalEffectAvgDps", "每秒平均伤害" },

                    }
                }
            };

        private const MyLanguagesEnum FallbackLanguage = MyLanguagesEnum.English;

        private static readonly MyLanguagesEnum Language = MyAPIGateway.Session.Config.Language;

        private static readonly Dictionary<string, string> FallbackI18NDictionary = I18NDictionaries[FallbackLanguage];

        private static readonly Dictionary<string, string> I18NDictionary =
            I18NDictionaries.GetValueOrDefault(Language, FallbackI18NDictionary);

        internal static bool HasText(string text)
        {
            return I18NDictionary.ContainsKey(text);
        }

        internal static string GetText(string text, params object[] args)
        {
            string value;
            if (!I18NDictionary.TryGetValue(text, out value))
            {
                if (!FallbackI18NDictionary.TryGetValue(text, out value))
                {
                    value = text;
                }
            }

            return args.Length == 0 ? value : string.Format(value, args);
        }

        internal static string GetTextWithoutFallback(string text)
        {
            string value;
            return I18NDictionary.TryGetValue(text, out value) ? value : text;
        }
    }
}