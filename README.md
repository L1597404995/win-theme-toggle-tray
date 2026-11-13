# Windows 深浅色主题托盘切换

一个常驻任务栏通知区的小工具：左键单击托盘图标即可切换 Windows 10/11 的深色 / 浅色主题，也支持全局快捷键和开机自动启动。没有主窗口，不会在桌面或任务栏按钮区留下任何东西。

## 功能

- 左键单击任务栏通知区图标，立即切换系统深浅色主题
- 图标随当前模式变化：浅色时显示太阳，深色时显示月亮
- 全局快捷键 `Ctrl+Alt+T`，在任意程序里都能切换
- 右键菜单可更换快捷键（`Ctrl+Alt+T` / `Ctrl+Alt+D` / `Alt+Shift+T` / 关闭）
- 右键菜单可开启「开机自动启动」
- 程序已运行时再次双击 exe，会通知现有实例切换一次，不会多开进程
- 设置保存在 `HKCU\Software\ThemeToggleWidget`，重启后保留

## 使用方法

1. 运行 [`dist/ThemeToggleWidget.exe`](dist/ThemeToggleWidget.exe)
2. 如果托盘里没看到图标，点击任务栏的 `^` 展开隐藏图标区
3. 左键单击图标切换主题；右键打开设置菜单

主题切换通过写入以下注册表值并广播 `WM_SETTINGCHANGE` 实现：

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize
    AppsUseLightTheme
    SystemUsesLightTheme
```

部分正在运行的应用需要重启后才能完全应用新主题，这是 Windows 自身的行为。

## 系统要求

- Windows 10 / Windows 11
- .NET Framework 4.7 或更高版本（Windows 10/11 已自带）

## 从源码构建

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建脚本会生成图标并调用系统自带的 `csc.exe` 编译，无需安装 Visual Studio 或 .NET SDK。输出文件为 `dist/ThemeToggleWidget.exe`。

## 项目结构

```
build.ps1            构建脚本（生成图标 + 编译）
src/Program.cs       全部源码（WinForms 托盘程序）
dist/                编译好的可执行文件
```

## 说明

- 全局快捷键依赖程序在后台运行，建议在右键菜单中勾选「开机自动启动」
- 如果快捷键被其它程序占用，托盘会弹出提示，在右键菜单里换一个组合即可
- 程序不联网，只读写当前用户的主题设置和启动项注册表
