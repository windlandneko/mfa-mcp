using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AlienBridge {
  static class Events {
    public static readonly string Session = Guid.NewGuid().ToString("N");
    static readonly List<JObject> history = new List<JObject>();
    static Dictionary<string, string> settings;
    static int sequence;

    public static void Add(string text) {
      lock (history) {
        history.Add(new JObject { ["seq"] = ++sequence, ["text"] = text });
        if (history.Count > 32)
          history.RemoveAt(0);
      }
    }

    static Dictionary<string, string> Settings() {
      var data = Global.globalData;
      var size = Global.screenOption[data.screensize];
      return new Dictionary<string, string> {
        ["Colors"] = "text #" + ColorUtility.ToHtmlStringRGB(Global.FrontColor()) + ", background #" + ColorUtility.ToHtmlStringRGB(Global.BackColor()),
        ["Display mode"] = data.screen == "full" ? "fullscreen" : "windowed",
        ["Window size"] = $"{size.x}×{size.y}",
        ["Save slot"] = data.slot.ToString()
      };
    }

    public static JArray Snapshot() {
      var current = Settings();
      if (settings != null) {
        foreach (var entry in current) {
          if (settings[entry.Key] != entry.Value)
            Add(entry.Key + " changed: " + entry.Value + ".");
        }
      }
      settings = current;
      lock (history)
        return new JArray(history);
    }
  }
}
