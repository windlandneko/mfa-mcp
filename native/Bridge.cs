using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

namespace AlienBridge {
  public static class Hooks {
    public static void EnsureAnnotation(AlienCharacter cell) {
      if (cell.showtxt && Global.savedata != null && !string.IsNullOrEmpty(cell.alien_txt)
          && !Global.savedata.dict.ContainsKey(cell.alien_txt))
        Global.savedata.dict[cell.alien_txt] = "?";
    }
    public static bool Active;
    public static Vector3 Pos;
    public static int Button;
    public static int QuitRequests, UrlRequests;
    public static string LastUrl;
    public static Vector3 MousePosition() => Active ? Pos : Input.mousePosition;
    public static bool GetMouseButtonDown(int b) => Active ? b == Button : Input.GetMouseButtonDown(b);
    public static string SaveDir() {
      var path = Environment.GetEnvironmentVariable("ALIEN_SAVE_DIR");
      if (string.IsNullOrEmpty(path))
        throw new InvalidOperationException("ALIEN_SAVE_DIR is required");
      Directory.CreateDirectory(path);
      return path;
    }
    public static void Quit() {
      Events.Add("Exit requested; kept the game open.");
      QuitRequests++;
    }
    public static void OpenUrl(string url) {
      Events.Add("External link blocked: " + url);
      UrlRequests++;
      LastUrl = url;
    }
    // Use the offline save slot.
    public static Steamworks.SteamId SteamId() => default;
    public static void Achievement(string id) => AchievementLedger.Unlock(id);
    static bool started;
    public static void Start(Draw d) {
      if (started) {
        Events.Add("Game restarted.");
        return;
      }
      started = true;
      Application.runInBackground = true;
      if (Application.isBatchMode) {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
      }
      Events.Snapshot();
      Server.Start(int.Parse(Environment.GetEnvironmentVariable("ALIEN_BRIDGE_PORT") ?? "8765"));
    }
    // Snapshot on the next Update: LateUpdate has refreshed notes and Destroy has removed old borders.
    static bool finished;
    public static void Pre(Draw d) {
      Active = false;
      if (Jobs.Current != null) {
        var previous = Jobs.Current;
        Jobs.Current = null;
        if (finished) {
          try {
            previous.Complete(previous.Post(d));
          }
          catch (Exception e) { previous.Fail(e); }
        }
        else
          previous.Fail(new Exception("game update failed"));
      }
      finished = false;
      while (Jobs.Queue.TryDequeue(out var job)) {
        if (!job.Begin())
          continue;
        Jobs.Current = job;
        try {
          job.Pre?.Invoke(d);
        }
        catch (Exception e) {
          Active = false;
          job.Fail(e);
          Jobs.Current = null;
        }
        break;
      }
    }
    public static void Post(Draw d) {
      Active = false;
      finished = true;
    }
  }

  class Job {
    public Action<Draw> Pre;
    public Func<Draw, object> Post;
    readonly ManualResetEventSlim done = new ManualResetEventSlim();
    int status;
    object result;
    Exception error;
    public bool Begin() => Interlocked.CompareExchange(ref status, 1, 0) == 0;
    public void Complete(object value) {
      result = value;
      done.Set();
    }
    public void Fail(Exception e) {
      error = e;
      done.Set();
    }
    public object Wait() {
      if (!done.Wait(10000)) {
        Interlocked.CompareExchange(ref status, 2, 0);
        throw new TimeoutException("game did not finish the request; inspect state before retrying");
      }
      if (error != null)
        throw new Exception(error.Message);
      return result;
    }
  }
  static class Jobs {
    public static readonly ConcurrentQueue<Job> Queue = new ConcurrentQueue<Job>();
    public static Job Current;
    public static object Run(Action<Draw> pre, Func<Draw, object> post) {
      var job = new Job { Pre = pre, Post = post };
      Queue.Enqueue(job);
      return job.Wait();
    }
  }
}
