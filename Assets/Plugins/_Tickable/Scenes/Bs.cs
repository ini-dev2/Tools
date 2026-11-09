using Tickable.Runtime;
using UnityEngine;

public class Bs : MonoBehaviour
{
    TestTick c1;
    TestTickTime c2;
    TestTickRate c3;

    ITickManager<IUpdateTick> tickManager = new UpdateTickManager();

    private void Awake()
    {
        c1 = new TestTick();
        c2 = new TestTickTime();
        c3 = new TestTickRate();

        tickManager.Register(TickType.NONE, c1);
        tickManager.Register(TickType.ON_SECONDS, c2, 5f);
        tickManager.Register(TickType.ON_TICKRATE, c3);
    }

    [ContextMenu("CallHook")]
    public void Call()
    {
        tickManager.CallHook();
    }

    [ContextMenu("StopClear")]
    public void Decall()
    {
        tickManager.Dispose();
    }

    [ContextMenu("Play")]
    public void Play()
    {
        tickManager.PlayHook();
    }

    [ContextMenu("Stop")]
    public void Pause()
    {
        tickManager.StopHook();
    }
}

public class TestTick : IUpdateTick
{
    public void Tick(float dTime)
    {
        Debug.Log("I am FREE tick");
    }
}

public class TestTickTime : IUpdateTick
{
    public void Tick(float dTime)
    {
        Debug.LogError("I am TIME tick");
    }
}

public class TestTickRate : IUpdateTick
{
    public void Tick(float dTime)
    {
        Debug.LogWarning("I am RATE tick");
    }
}
