# Message From Aliens MCP

通过四个 MCP 工具游玩 Steam《外星来信》，面向 Windows。工具返回文字网格，`render` 按需返回字形图片。

## 启动

安装游戏、[uv](https://docs.astral.sh/uv/getting-started/installation/) 和 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，在项目目录打开 PowerShell：

```powershell
uv run build.py --game 'D:\SteamLibrary\steamapps\common\Message From Aliens'
uv run run.py
```

`build.py` 将游戏复制到 `game/` 并应用补丁，原安装目录保持不变。`--game` 也接受游戏 `.exe` 路径。游戏更新后，用 `--output game-new` 重新构建。

默认后台运行，独立存档位于 `saves/default/`，不连接 Steam 云存档。需要窗口或另一份存档：

```powershell
uv run run.py --window --save 'D:\AlienSaves\new-run'
```

## 连接 MCP

在客户端添加 stdio 服务；把路径改成项目的实际位置：

```json
{
  "mcpServers": {
    "alien": {
      "command": "uv",
      "args": ["run", "--project", "C:\\mfa-mcp", "C:\\mfa-mcp\\mcp.py"]
    }
  }
}
```

先启动游戏，再连接客户端。需要调试日志时，给 `mcp.py` 加 `--log logs/tools.jsonl`。

| 工具 | 参数 | 用途 |
|---|---|---|
| `click` | `{x,y}` | 左键操作并返回画面 |
| `rclick` | `{x,y}` | 右键操作并返回画面 |
| `note` | `{notes:[{id,text}]}` | 注释并返回画面；空数组读取当前画面 |
| `render` | `{ids:["AB","94"]}` | 返回字形 PNG，保留顺序和重复项 |

坐标从左上角 `(0,0)` 开始，范围为 24×9。每条注释最多 20 个字符，空文本清除注释。字形 ID 为两个英文字母或两个数字，区分大小写。

首次读取画面时附带成就列表和解锁状态；新成就显示编号、名称及说明。成就记录随独立存档保留。

退出、外链和设置变化以文字事件返回；退出和外链不实际执行。字形 ID 映射随独立存档保存。

## 文件结构

- `mcp.py`、`mcp_tools/`：工具声明、调用、网格及图片输出、stdio 协议。
- `build.py`、`native/`：Unity Mono 桥接及补丁。
- `run.py`：启动游戏、选择存档和端口。
- `font/`：字形字体及映射数据。

构建需要游戏目录中的 `Managed/Assembly-CSharp.dll`，支持 Unity Mono 版。启动失败时查看存档目录的 `player.log`。

默认桥接地址为 `127.0.0.1:8765`。多开时分别设置 `run.py --port 8766` 和 `mcp.py --bridge http://127.0.0.1:8766`。同一端口已有游戏时，启动器只连接相同存档。

如果日志提示 `HttpListenerException: Access is denied`，在管理员 PowerShell 中为当前用户配置一次端口权限，再正常启动：

```powershell
netsh http add urlacl url=http://127.0.0.1:8765/ user="$env:USERDOMAIN\$env:USERNAME"
```

## 许可

代码采用 [MIT](LICENSE)。`font/` 字体及字形素材沿用原声明，不适用 MIT，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
