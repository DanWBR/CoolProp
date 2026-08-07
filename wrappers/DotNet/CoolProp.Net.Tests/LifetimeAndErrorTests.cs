namespace CoolProp.Net.Tests;

/// <summary>
/// Handle ownership and the two native failure conventions.
/// </summary>
[Collection(NativeLibraryCollection.Name)]
public sealed class LifetimeAndErrorTests
{
    [Fact]
    public void DisposingTwiceIsSafe()
    {
        AbstractState state = AbstractState.Create("HEOS", "Water");
        state.Dispose();

        // Must not throw and must not free the native handle a second time.
        state.Dispose();
    }

    [Fact]
    public void UsingAfterDisposeThrowsObjectDisposed()
    {
        AbstractState state = AbstractState.Create("HEOS", "Water");
        state.Dispose();

        Assert.Throws<ObjectDisposedException>(() => state.Update("PT_INPUTS", 101325, 300));
    }

    /// <summary>
    /// Proves the message buffer is wired, not just the error code: a bare
    /// errcode check would pass with an empty message.
    /// </summary>
    [Fact]
    public void InvalidBackendThrowsWithANativeMessage()
    {
        CoolPropException ex = Assert.Throws<CoolPropException>(
            () => AbstractState.Create("NOT_A_BACKEND", "Water"));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        Assert.Contains("NOT_A_BACKEND", ex.Message);
        Assert.NotEqual(0, ex.ErrorCode);
    }

    [Fact]
    public void InvalidFluidThrowsWithANativeMessage()
    {
        CoolPropException ex = Assert.Throws<CoolPropException>(
            () => AbstractState.Create("HEOS", "NotARealFluid"));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    /// <summary>
    /// The scalar entry points signal failure with a non-finite return rather
    /// than an error code. The binding must surface that as an exception, never
    /// hand back an infinity for a caller to propagate into a simulation.
    /// </summary>
    [Fact]
    public void FailingPropsSIThrowsInsteadOfReturningASentinel()
    {
        CoolPropException ex = Assert.Throws<CoolPropException>(
            () => Props.PropsSI("T", "P", -1, "Q", 0, "Water"));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void FailingProps1SIThrowsInsteadOfReturningASentinel()
    {
        Assert.Throws<CoolPropException>(() => Props.Props1SI("NotARealFluid", "Tcrit"));
    }

    [Fact]
    public void UnknownParameterNameIsRejectedBeforeTheNativeCall()
    {
        using AbstractState state = AbstractState.Create("HEOS", "Water");
        state.Update("PT_INPUTS", 101325, 300);

        CoolPropException ex = Assert.Throws<CoolPropException>(
            () => state.KeyedOutput("not_a_parameter"));
        Assert.Contains("not_a_parameter", ex.Message);
    }

    [Fact]
    public void UnknownInputPairIsRejectedBeforeTheNativeCall()
    {
        using AbstractState state = AbstractState.Create("HEOS", "Water");

        CoolPropException ex = Assert.Throws<CoolPropException>(
            () => state.Update("NOT_A_PAIR", 1, 2));
        Assert.Contains("NOT_A_PAIR", ex.Message);
    }

    [Fact]
    public void ImpossibleStateThrowsFromTheAbstractStateConvention()
    {
        using AbstractState state = AbstractState.Create("HEOS", "Water");

        // Negative absolute pressure and temperature cannot be solved.
        Assert.Throws<CoolPropException>(() => state.Update("PT_INPUTS", -101325, -300));
    }

    [Fact]
    public void ManyCreateDisposeCyclesDoNotExhaustTheHandleTable()
    {
        for (int i = 0; i < 500; i++)
        {
            using AbstractState state = AbstractState.Create("HEOS", "Water");
            state.Update("PT_INPUTS", 101325, 300);
        }
    }

    [Fact]
    public void FinalizerReleasesAnUndisposedHandle()
    {
        WeakReference reference = CreateAndAbandon();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(reference.IsAlive);

        static WeakReference CreateAndAbandon()
        {
            AbstractState state = AbstractState.Create("HEOS", "Water");
            state.Update("PT_INPUTS", 101325, 300);
            return new WeakReference(state);
        }
    }
}
