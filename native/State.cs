using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AlienBridge {
  static class State {
    const int W = 24, H = 9;
    static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < 0.02f;

    public static AlienCharacter Slot(Draw d, int x, int row) => d.alien_slots[x, H - 1 - row];

    public static JObject Snapshot(Draw d) {
      var cells = new JArray();
      for (int row = 0; row < H; row++)
        for (int x = 0; x < W; x++) {
          var ac = Slot(d, x, row);
          if (ac == null || !ac.gameObject.activeSelf)
            continue;
          var c = new JObject { ["x"] = x, ["y"] = row };
          bool blank = ac.sp != null && !ac.sp.enabled;
          if (!blank)
            c["id"] = Ids.Of(ac.alien_txt);
          else
            c["blank"] = true;
          if (ac.showtxt && !blank && !Global.globalData.hidetranslation) {
            var n = ac.txt.text;
            if (n == "?")
              n = null;
            if (n != null)
              c["note"] = n;
            c["editable"] = true;
          }
          if (ac.background != null && ac.background.gameObject.activeSelf)
            c["hl"] = true;
          if (ac.sp != null && Same(ac.sp.color, d.DefaultColor(3)))
            c["dim"] = true;
          cells.Add(c);
        }

      var marks = new JArray();
      if (d.selected != null)
        foreach (var lr in d.selected) {
          if (lr == null)
            continue;
          var col = lr.startColor;
          string style = Geometry.Style(d, col);
          if (lr.positionCount == 2 && Mathf.Abs(lr.GetPosition(0).y - lr.GetPosition(1).y) < .02f) {
            Vector3 a = lr.GetPosition(0), b = lr.GetPosition(1);
            if (a.x > b.x) {
              var t = a;
              a = b;
              b = t;
            }
            int x0 = Mathf.FloorToInt(a.x + 12f + 0.01f), x1 = Mathf.CeilToInt(b.x + 12f - 0.01f) - 1;
            int j = Mathf.RoundToInt((a.y + 6.70f) / 1.5f);
            marks.Add(new JObject { ["type"] = "underline", ["x0"] = x0, ["x1"] = x1, ["y"] = H - 1 - j, ["style"] = style });
          }
          else if (lr.positionCount == 5) {
            Vector3 c0 = lr.GetPosition(0), c2 = lr.GetPosition(2);
            var ctr = (c0 + c2) / 2f;
            float h = Mathf.Abs(c0.y - c2.y);
            int x = Mathf.FloorToInt(ctr.x + 12f);
            int j = Mathf.RoundToInt((ctr.y + (h > 1.5f ? 1f : 0f) - 1f + 6.75f) / 1.5f);
            var m = new JObject { ["type"] = "box", ["x"] = x, ["y"] = H - 1 - j, ["style"] = style };
            if (h > 1.5f)
              m["tall"] = true;
            marks.Add(m);
          }
        }

      var o = new JObject { ["frame"] = Time.frameCount, ["cells"] = cells, ["marks"] = marks };
      o["geometry"] = Geometry.Read(d);
      o["session"] = Events.Session;
      o["slot"] = Global.globalData.slot;
      o["achievements"] = Stats.Read()?["earned"] ?? new JArray();
      o["events"] = Events.Snapshot();
      o["quit_requests"] = Hooks.QuitRequests;
      o["url_requests"] = Hooks.UrlRequests;
      if (Hooks.LastUrl != null)
        o["last_url"] = Hooks.LastUrl;
      return o;
    }

  }
}
