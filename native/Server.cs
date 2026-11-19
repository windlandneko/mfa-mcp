using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AlienBridge {
  static class Server {
    public static void Start(int port) {
      var listener = new HttpListener();
      listener.Prefixes.Add($"http://127.0.0.1:{port}/");
      listener.Start();
      new Thread(() => {
        while (listener.IsListening) {
          var context = listener.GetContext();
          ThreadPool.QueueUserWorkItem(_ => Handle(context));
        }
      }) {
        IsBackground = true,
        Name = "AlienBridge"
      }.Start();
    }

    static void Handle(HttpListenerContext context) {
      object result;
      string body;
      try {
        body = new StreamReader(context.Request.InputStream, Encoding.UTF8).ReadToEnd();
        var args = body.Length == 0 ? new JObject() : JObject.Parse(body);
        foreach (var key in context.Request.QueryString.AllKeys) {
          if (key != null)
            args[key] = context.Request.QueryString[key];
        }
        result = Route(context.Request.Url.AbsolutePath, args);
      }
      catch (Exception error) {
        context.Response.StatusCode = error is ArgumentException ? 400 : 500;
        result = new {
          error = error.Message
        };
      }
      var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
      context.Response.ContentType = "application/json; charset=utf-8";
      context.Response.ContentLength64 = bytes.Length;
      try {
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
      }
      catch (HttpListenerException) {
        // The MCP client can disconnect while Unity is finishing a frame.
      }
      finally {
        context.Response.Close();
      }
    }

    static int Coordinate(JObject args, string key, int limit) {
      if (!int.TryParse((string)args[key], out var value) || value < 0 || value >= limit)
        throw new ArgumentException($"{key} must be an integer in 0..{limit - 1}");
      return value;
    }

    static object Route(string path, JObject args) {
      switch (path) {
        case "/health":
          return new {
            api = 2,
            pid = System.Diagnostics.Process.GetCurrentProcess().Id,
            steam_initialized = Steamworks.SteamClient.IsValid,
            save_dir = Hooks.SaveDir()
          };
        case "/state":
          return Jobs.Run(null, State.Snapshot);
        case "/click":
          int x = Coordinate(args, "x", 24), y = Coordinate(args, "y", 9);
          string button = (string)args["button"];
          if (button != "left" && button != "right")
            throw new ArgumentException("button must be left or right");
          return Jobs.Run(draw => {
            var cell = State.Slot(draw, x, y);
            Hooks.Pos = Camera.main.WorldToScreenPoint(cell.transform.position);
            Hooks.Button = button == "left" ? 0 : 1;
            Hooks.Active = true;
          }, State.Snapshot);
        case "/annotate":
          string id = (string)args["id"] ?? throw new ArgumentException("missing id");
          string text = (string)args["text"] ?? throw new ArgumentException("missing text");
          if (new System.Globalization.StringInfo(text).LengthInTextElements > 20)
            throw new ArgumentException("notes are limited to 20 characters");
          return Jobs.Run(draw => {
            var word = Ids.Word(id);
            var cell = draw.alien_slots.Cast<AlienCharacter>().FirstOrDefault(candidate =>
              candidate.gameObject.activeSelf && candidate.showtxt && candidate.sp.enabled && candidate.alien_txt == word);
            if (cell == null)
              throw new ArgumentException(id + " has no editable note on this screen");
            cell.Init();
            cell.txt.text = text;
            cell.EndEdit(text);
          }, State.Snapshot);
        case "/glyph":
          return Jobs.Run(null, draw => {
            var code = (string)args["id"] ?? throw new ArgumentException("missing id");
            var word = Ids.Word(code);
            return new {
              id = code,
              unicode = Ids.Unicode(word),
              strokes = Global.StringToCharacter(word).Select(stroke =>
                stroke.Select(point => new[] { Math.Round(point.x, 3), Math.Round(point.y, 3) }))
            };
          });
        default:
          throw new ArgumentException("unknown route");
      }
    }
  }
}
