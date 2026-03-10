# 任务目标：WPF 全局字体资源标准化与 XAML 深度重构

作为高级 WPF 架构师，请帮我清理和重构当前项目中的字体大小（FontSize）资源。当前项目（基于 .NET 8 + Prism 9 的模块化工业应用）的 Default.xaml 中由于工具生成或历史遗留，存在大量冗余、命名混乱且未被充分利用的字体资源。

---

## 1. 现状分析

在 Default.xaml 中，存在两套或多套平行的字体定义，例如：

- **体系A**：`LargeFontSize` (24), `HeadFontSize` (20), `SubHeadFontSize` (16), `TextFontSize` (14)
- **体系B**：`MaxTitleFontSize` (22), `TitleFontSize` (20), `SecondTitleFontSize` (18), `ThirdTitleFontSize` (16), `ForthTitleFontSize` (14), `FivethTitleFontSize` (11)

此外，各个模块（如 `PF.Modules.*`, `PF.Workstation.*`, `PF.Application.*`）的 XAML 文件中，可能存在大量**硬编码**的 `FontSize="14"` 或对混乱资源的随意引用。

---

## 2. 重构规范（单一真理来源）

请将 Default.xaml 中的字体资源统一合并为以下一套**语义化、层级清晰**的标准命名（如果你认为有更符合现代工业 UI 规范的命名，可以调整，但必须保持精简和统一）：

| 资源 Key | 值 | 用途 |
|----------|-----|------|
| `FontSize.H1.Large` | 24 | 顶级标题 / 巨型数据展示 |
| `FontSize.H2.Title` | 20 | 页面标题 / 模块标题 |
| `FontSize.H3.Subtitle` | 16 | 次级标题 / 分组标题 |
| `FontSize.Body.Standard` | 14 | 全局默认正文（DataGrid、普通文本） |
| `FontSize.Body.Small` | 12 | 辅助说明 / 日志 / 图例 |
| `FontSize.Caption.Mini` | 11 | 极小字体的状态提示 / 角标 |

---

## 3. 执行步骤（严格按顺序执行）

### 第零步：备份与分支

1. 创建 Git 分支 `refactor/font-size-standardization`
2. 确保当前代码已提交

---

### 第一步：清理并更新字典

1. 定位到包含这些旧字体的资源字典（通常是 `PF.UI.Resources/Themes/Default.xaml` 或类似路径）。
2. 删除旧的、混乱的字体 `<system:Double>` 定义。
3. 注入上述最新的标准化字体定义。

---

### 第二步：全局映射与替换（动态资源）

1. 遍历解决方案中所有的 `.xaml` 文件（重点关注 Views 文件夹和 `UserControl`）。
2. 将原本绑定了旧资源名称（如 `{DynamicResource HeadFontSize}` 或 `{DynamicResource TitleFontSize}`）的代码，安全地替换为对应的新资源（如 `{DynamicResource FontSize.H2.Title}`）。
3. **请确保使用 `DynamicResource` 以支持未来的动态主题切换。**

**映射关系**：

| 旧 Key | 新 Key |
|--------|--------|
| `LargeFontSize` | `FontSize.H1.Large` |
| `HeadFontSize` | `FontSize.H2.Title` |
| `TitleFontSize` | `FontSize.H2.Title` |
| `MaxTitleFontSize` | `FontSize.H2.Title` |
| `SubHeadFontSize` | `FontSize.H3.Subtitle` |
| `SecondTitleFontSize` | `FontSize.H3.Subtitle` |
| `ThirdTitleFontSize` | `FontSize.H3.Subtitle` |
| `TextFontSize` | `FontSize.Body.Standard` |
| `ForthTitleFontSize` | `FontSize.Body.Standard` |
| `FivethTitleFontSize` | `FontSize.Caption.Mini` |

---

### 第三步：消除硬编码（Hardcode Eradication）

1. 使用正则或代码搜索，找出 XAML 中硬编码的 `FontSize="XX"`。
2. 将常见的硬编码值替换为标准引用：

| 硬编码值 | 替换为 |
|---------|--------|
| `FontSize="24"` | `FontSize="{DynamicResource FontSize.H1.Large}"` |
| `FontSize="20"` / `FontSize="22"` | `FontSize="{DynamicResource FontSize.H2.Title}"` |
| `FontSize="16"` / `FontSize="18"` | `FontSize="{DynamicResource FontSize.H3.Subtitle}"` |
| `FontSize="14"` | `FontSize="{DynamicResource FontSize.Body.Standard}"` |
| `FontSize="12"` | `FontSize="{DynamicResource FontSize.Body.Small}"` |
| `FontSize="11"` | `FontSize="{DynamicResource FontSize.Caption.Mini}"` |

---

### 第四步：验证与检查

1. 检查是否存在通过代码后台（C#）获取这些旧资源键名的情况（如 `FindResource("TextFontSize")`），如果有，同步修改。
2. 确保 XML 命名空间（如 `xmlns:system="clr-namespace:System;assembly=mscorlib"`）在资源字典中未被破坏。
3. 启动应用程序，快速浏览所有主要界面，确保无资源查找失败异常。

---

## 4. 注意事项

- **第三方控件库**：如果使用了 HandyControl、MaterialDesignInXaml 等，它们有自己的字体资源，**不要动**。
- **模板绑定**：`ControlTemplate` 里的 `TemplateBinding FontSize` 是内部绑定，不需要改。
- **动画**：如果有动画改变 FontSize，硬编码值可能是故意的，请谨慎处理。
- **特殊控件**：遇到继承特定属性的自定义控件，请谨慎处理并提示。

---

## 5. 执行报告

请逐步执行，并在每完成一个步骤后向我简报修改的文件数量。

| 步骤 | 修改文件数 | 状态 |
|------|-----------|------|
| 第零步：备份与分支 | - | ✅ 已完成（分支：refactor/font-size-standardization） |
| 第一步：清理字典 | 2 | ✅ 已完成（09_Sizes.xaml, 04_Fonts.xaml） |
| 第二步：映射替换 | 4 | ✅ 已完成（TextBlock.xaml, 11_ControlBaseStyle.xaml, MainWindow.xaml, DateTimeSelector.xaml） |
| 第三步：消除硬编码 | 14 | ✅ 已完成（批量替换 13 个 View 文件） |
| 第四步：验证检查 | - | ✅ 已完成（编译成功，0 错误） |
| **额外**：Default.xaml | 1 | ✅ 已完成（手动更新合并文件） |

**总计修改文件**：21 个 XAML 文件 + 1 个需求文档

---

**创建时间**：2026-03-10
**项目**：PF.AutoFramework
**预计工作量**：2-4 小时
**风险等级**：中等（涉及全局 UI 资源）
