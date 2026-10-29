# 启动器首页第二版设计复核

日期：2026-10-26。范围：现有原生 Android 首页按钮布局与样式。

- Source visual truth: `docs/design/launcher-home-grid.png`，941×1672。
- Implementation screenshot: `artifacts/launcher/home-grid-final.png`，1080×2460；物理 density 2.75，逻辑宽约 393dp。
- Viewport normalization: 移除原生系统栏后以等宽、保持比例方式比较；两者长宽比不同，不拉伸图像。原生背景继续沿用原有 centerCrop、状态和页脚的位置约束；只对按钮区域判断本次改版保真度。
- State: 可启动，字体 1.0，所有八个按钮可见；实际安装插件版本说明保留。
- Full-view comparison: `artifacts/launcher/home-grid-comparison-final.png`。
- Focused comparison: `artifacts/launcher/home-grid-buttons-final.png`；左为原型，右为最终原生按钮，缩放保持比例。

## Findings

没有剩余 P0/P1/P2 按钮布局问题。保留原生控件、既有 64dp 主按钮触控区域及系统字体，因此按钮比例、文字抗锯齿与原型生成文字不逐像素相同。背景、状态与署名不在本次改版范围，复用已有实现。

- Fonts and typography：系统中文 14sp 网格标签、24dp 图标与原有主按钮字重；文字无截断，130% 字体完整；品牌仍用现有 serif。
- Spacing and layout rhythm：全宽主按钮、2×3 等宽网格、8dp 间隔和底部全宽更多入口，视觉顺序与选定图一致；其他页面未套用首页样式。
- Colors and tokens：暖黑、深棕紫填充、奶油色标签、暖金边框沿用项目 token，只有启动按钮使用琥珀实心强调。
- Image and icon fidelity：背景使用原项目素材，图标为已有资源及 Material Design Icons 原始 path 转换的 VectorDrawable；来源及许可证随包可读。
- Copy and content：原有八个入口及全部处理函数保留；真实状态、副标题、版本和署名沿用产品实际文案。

## Comparison history

初次按钮对比发现网格 76dp 高度导致按钮区偏长，作为 P2 修正为 64dp，垂直 padding 10dp 改为 6dp。重新构建／覆盖安装，最终截图及 focused comparison 确认布局收紧且标签完整。

## Verification and limits

13 项既有离线回归、签名构建、覆盖安装通过；六个功能入口与更多／许可的跳转通过；130% 字体及实际横屏滚动截图人工复核通过。横屏 Android CLI 服务会回到竖屏，横屏依据 ADB 截图，未宣称有横屏布局树自动断言。入口逻辑未变，本次未启动游戏、执行修复或付款。第二设备、更大字体与分屏未测试。

## Follow-up polish

P3：原型更多按钮末端的装饰箭头未加入，保留工具图标、文案与现有点击反馈。

final result: passed
