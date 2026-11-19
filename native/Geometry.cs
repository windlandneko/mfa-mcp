using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AlienBridge {
  static class Geometry {
    static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g)
      + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < .02f;
    static object Point(Vector3 p) => new[] { Math.Round(p.x, 4), Math.Round(p.y, 4) };
    public static string Style(Draw d, Color c) {
      if (Same(c, d.DefaultColor(3)))
        return "grey";
      if (Same(c, d.DefaultColor(2)))
        return "faint";
      return "solid";
    }
    public static JArray Read(Draw d) {
      var result = new JArray();
      foreach (var line in UnityEngine.Object.FindObjectsByType<LineRenderer>()) {
        if (!line.enabled || !line.gameObject.activeInHierarchy || line.startColor.a <= 0)
          continue;
        var points = new List<object>();
        for (int i = 0; i < line.positionCount; i++) {
          var v = line.GetPosition(i);
          points.Add(Point(line.useWorldSpace ? v : line.transform.TransformPoint(v)));
        }
        result.Add(JObject.FromObject(new {
          kind = "path",
          points,
          style = Style(d, line.startColor),
          selected = d.selected != null && d.selected.Contains(line) && (line.positionCount == 5 || (line.positionCount == 2 && Mathf.Abs(line.GetPosition(0).y - line.GetPosition(1).y) < .02f)),
          width = line.startWidth
        }));
      }
      foreach (var shape in UnityEngine.Object.FindObjectsByType<Shapes2D.Shape>()) {
        if (!shape.enabled || !shape.gameObject.activeInHierarchy || shape.GetComponentInParent<AlienCharacter>() != null)
          continue;
        var renderer = shape.GetComponent<Renderer>();
        if (renderer == null || !renderer.enabled || shape.settings.outlineColor.a <= 0)
          continue;
        var cfg = shape.settings;
        var points = new List<object>();
        string kind = "path";
        if (cfg.shapeType == Shapes2D.ShapeType.Rectangle) {
          foreach (var v in new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f), new Vector3(-.5f, -.5f) })
            points.Add(Point(shape.transform.TransformPoint(v)));
        }
        else if (cfg.shapeType == Shapes2D.ShapeType.Polygon) {
          foreach (var v in cfg.polyVertices)
            points.Add(Point(shape.transform.TransformPoint(v)));
          if (points.Count > 0)
            points.Add(points[0]);
        }
        else if (cfg.shapeType == Shapes2D.ShapeType.Ellipse) {
          for (int i = 0; i <= 48; i++) {
            float a = i * Mathf.PI * 2 / 48;
            points.Add(Point(shape.transform.TransformPoint(new Vector3(Mathf.Cos(a) / 2, Mathf.Sin(a) / 2))));
          }
          kind = "ellipse";
        }
        else
          continue;
        result.Add(JObject.FromObject(new {
          kind,
          points,
          style = Style(d, cfg.outlineColor),
          fill = cfg.fillType != Shapes2D.FillType.None,
          width = cfg.outlineSize
        }));
      }
      return result;
    }
  }
}
