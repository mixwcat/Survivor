# 防御塔放置系统重构 - Unity Editor 手动操作指南

## 代码修改已完成 ✓

以下文件已自动更新：
- ✓ `Assets/Script/Entity/Tower/PlaceTowerLogic.cs` → 重命名为 `TowerPlacementController.cs`
- ✓ `Assets/Script/UI/GamePanel/GamePanel.cs` - 添加了确认/取消按钮支持
- ✓ `Assets/Script/UI/GamePanel/ChooseTowerPanel.cs` - 直接生成塔放置精灵
- ✓ `Assets/Script/InputSystem/TowerSpriteController.cs` - 已删除（逻辑已合并）

## Unity Editor 手动操作清单

### 1. 更新 GamePanel 预制体（必需）

**文件路径**: `Assets/Resources/UI/GamePanel.prefab`

**操作步骤**:
1. 在 Unity Editor 中打开 `GamePanel` 预制体
2. 在右上角添加两个 UI Button 元素：
   - 按钮 1：命名为 `btnPlaceTowerConfirm`
     - Text: "确认" 或使用确认图标
     - 建议位置：右上角，`btnSetting` 按钮下方
   - 按钮 2：命名为 `btnPlaceTowerCancel`  
     - Text: "取消" 或使用取消图标
     - 建议位置：`btnPlaceTowerConfirm` 按钮下方
3. 将两个按钮的引用拖拽到 `GamePanel` 脚本组件的对应字段：
   - `btnPlaceTowerConfirm` → GamePanel.btnPlaceTowerConfirm
   - `btnPlaceTowerCancel` → GamePanel.btnPlaceTowerCancel
4. 设置两个按钮的初始状态为 **不可见**：
   - 取消勾选 GameObject 的 Active 复选框（设为 `SetActive(false)`）
   - 或者在 Inspector 中设置 CanvasGroup.alpha = 0
5. 保存预制体

**注意**: 
- 按钮仅在移动端显示（代码中有 `#if UNITY_ANDROID` 控制）
- PC 端这两个按钮会始终隐藏

### 2. 更新 SpriteToHandle 预制体（必需）

**文件路径**: `Assets/Resources/Prefabs/SpriteToHandle.prefab`

**操作步骤**:
1. 在 Unity Editor 中打开 `SpriteToHandle` 预制体
2. 查看挂载的脚本组件
3. 如果脚本组件显示为 `Missing` 或 `PlaceTowerLogic`：
   - 移除旧的脚本组件
   - 添加新的 `TowerPlacementController` 脚本组件
4. 确保以下组件存在：
   - `SpriteRenderer`（用于显示塔图标）
   - `CircleCollider2D` 或 `BoxCollider2D`（用于碰撞检测）
   - `Rigidbody2D`（如果需要触发器，设为 Kinematic）
5. 保存预制体

### 3. 场景中的引用检查（推荐）

**场景文件**: `Assets/Scenes/Level0.unity`

**操作步骤**:
1. 打开 `Level0` 场景
2. 在 Hierarchy 中找到包含 `GamePanel` 的 Canvas
3. 检查场景中是否有直接引用 `TowerSpriteController` 的 GameObject：
   - 使用 Edit → Find References In Scene 搜索
   - 如果找到，删除该 GameObject（已废弃）
4. 保存场景

### 4. 编译检查（必需）

**操作步骤**:
1. 打开 Unity Editor
2. 等待自动编译完成
3. 检查 Console 是否有错误：
   - ✓ 无错误：继续下一步
   - ✗ 有错误：
     - 如果提示 `Missing script reference`，按照步骤 2 更新预制体
     - 如果提示 `The type or namespace name 'TowerSpriteController' could not be found`，检查是否有遗漏的引用

### 5. 功能测试

#### PC 端测试（Windows）
1. 确保 Build Settings → Platform 设为 **PC, Mac & Linux Standalone**
2. 进入 Play Mode
3. 点击 `GamePanel` 的 `btnTowerShop` 按钮打开塔选择面板
4. 点击任意塔图标，验证：
   - ✓ 塔精灵在鼠标位置生成
   - ✓ 精灵随鼠标移动（吸附到网格）
   - ✓ 攻击范围圆环正确显示
   - ✓ 与 Player/Enemy/Tower 碰撞时变红，无碰撞时变绿
   - ✓ **左键点击**：扣除经验点，生成真实塔
   - ✓ **右键点击**：不扣除经验点，精灵消失

#### 移动端测试（Android）
1. 切换 Build Settings → Platform 为 **Android**
2. 等待编译完成
3. 在 Editor 中测试（模拟触摸）或真机测试：
   - 点击塔图标，验证：
     - ✓ 塔精灵在**屏幕中央**生成
     - ✓ 手指滑动时，精灵随手指**拖拽移动**（非瞬移）
     - ✓ **右上角显示确认/取消按钮**
     - ✓ 点击**确认按钮**：扣除经验点，生成真实塔，按钮消失
     - ✓ 点击**取消按钮**：不扣除经验点，精灵消失，按钮消失

### 6. 回归测试（推荐）

验证其他功能未受影响：
- ✓ 玩家移动（WASD/摇杆）
- ✓ 武器攻击（GunWeapon 旋转）
- ✓ `GamePanel.btnTowerLevelUp` 的显示/隐藏逻辑
- ✓ 其他 UI Panel 的打开/关闭

## 常见问题排查

### Q1: Console 提示 "NullReferenceException: Object reference not set to an instance of an object" 在 `GamePanel.SetTowerPlacementButtonsActive`
**原因**: `GamePanel` 预制体中的按钮引用未设置  
**解决**: 按照步骤 1 添加按钮并设置引用

### Q2: 点击塔图标后没有反应
**原因**: `SpriteToHandle` 预制体未更新脚本组件  
**解决**: 按照步骤 2 更新预制体脚本

### Q3: 移动端按钮没有显示
**原因**: 代码中有 `#if UNITY_ANDROID` 编译宏控制  
**解决**: 确保 Build Settings 平台设为 Android，重新编译

### Q4: 塔精灵的移动逻辑不正确（瞬移而非拖拽）
**原因**: 可能是 `TowerPlacementController` 代码未正确编译  
**解决**: 检查 `Update()` 方法中的 `#if UNITY_ANDROID` 分支

## 架构改进总结

### 改进点
1. **删除中间层**: 移除了 `TowerSpriteController`，简化了调用链
2. **平台差异化**: PC 和移动端使用不同的移动逻辑（直接设置 vs 拖拽）
3. **经验点扣除时机**: 从选择时扣除改为放置时扣除，逻辑更清晰
4. **移动端友好**: 添加了 UI 确认/取消按钮，解决了移动端无法取消的问题
5. **命名规范**: `PlaceTowerLogic` → `TowerPlacementController`，名称更准确

### 调用链对比

**旧流程** (两阶段点击):
```
ChooseTowerPanel 点击 
→ TowerSpriteController.HandleSprite() 
→ 等待用户点击屏幕
→ 实例化 PlaceTowerLogic 
→ 拖动 → 放置/取消
```

**新流程** (直接生成):
```
ChooseTowerPanel 点击 
→ 直接实例化 TowerPlacementController 
→ 拖动 → 放置/取消
```

## 完成检查清单

- [ ] GamePanel 预制体添加了两个按钮并设置引用
- [ ] SpriteToHandle 预制体更新了脚本组件
- [ ] Unity 编译无错误
- [ ] PC 端测试通过
- [ ] 移动端测试通过（如适用）
- [ ] 回归测试通过

完成后可删除此文件。
