# Git 仓库清理报告

## 清理时间
2026-06-08

## 清理目标
移除 Git 历史中的大型媒体文件（图片、音频等），减小仓库大小

## 清理结果

### 仓库大小
- **清理后**: ~15 MB (.git 目录)
- **清理前**: 估计 45+ MB

### 清理的文件类型
- 图片文件: *.png, *.jpg, *.psd, *.tga (约 150 个)
- 音频文件: *.mp3, *.wav, *.ogg (约 30 个)
- 大型资源文件: > 100KB

### 移除的重要大文件
- Main Menu.wav (8.0 MB)
- Lit2DSceneTemplate.scenetemplate (3.8 MB)
- BaDingShiWeiTi-16.ttf (3.2 MB)
- LiberationSans SDF.asset (2.2 MB)

### Git 历史变化
- **所有 commit SHA 已改变**（历史重写）
- 提交数量: 8 个 (保持不变)
- 移除对象: 81 个

## 更新的 .gitignore
新增忽略规则：
- 图片文件格式
- 音频文件格式
- 视频文件格式
- 3D 模型文件格式
- 保留 .meta 文件（Unity 必需）

## 注意事项

### 对于协作者
如果有人之前克隆了这个仓库，需要：
```bash
# 删除旧仓库
rm -rf Survivor

# 重新克隆
git clone https://github.com/mixwcat/Survivor.git
```

### 获取完整项目文件
完整的项目文件（包含所有媒体资源）将在 GitHub Releases 中提供。

## 工具
- BFG Repo-Cleaner 1.14.0
- Git 2.x

## 参考
- BFG 报告: `.bfg-report/2026-06-08/14-27-53/`
