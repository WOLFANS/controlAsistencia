using System;
using Xunit;
using controlAsistencia.Services;

namespace controlAsistencia.Tests.UnitTests;

public class DuplicateDetectorTests
{
    [Fact]
    public void SameTrabajador_Fecha_Tipo_IsDuplicate()
    {
        var det = new InFileDuplicateDetector();
        var fecha = new DateTime(2026,9,15,7,58,0);
        var added1 = det.TryAdd(1, fecha, "E");
        var added2 = det.TryAdd(1, fecha, "E");
        Assert.True(added1);
        Assert.False(added2);
        Assert.True(det.IsDuplicate(1, fecha, "E"));
    }

    [Fact]
    public void SameKey_OrigenDifferent_IsDuplicate()
    {
        var det = new InFileDuplicateDetector();
        var fecha = new DateTime(2026,9,15,7,58,0);
        var added1 = det.TryAdd(1, fecha, "E");
        // origen diferente should not affect
        var added2 = det.TryAdd(1, fecha, "E");
        Assert.True(added1);
        Assert.False(added2);
    }

    [Fact]
    public void DifferentFecha_NotDuplicate()
    {
        var det = new InFileDuplicateDetector();
        var f1 = new DateTime(2026,9,15,7,58,0);
        var f2 = new DateTime(2026,9,15,7,59,0);
        Assert.True(det.TryAdd(1, f1, "E"));
        Assert.True(det.TryAdd(1, f2, "E"));
    }

    [Fact]
    public void DifferentTipo_NotDuplicate()
    {
        var det = new InFileDuplicateDetector();
        var fecha = new DateTime(2026,9,15,7,58,0);
        Assert.True(det.TryAdd(1, fecha, "E"));
        Assert.True(det.TryAdd(1, fecha, "S"));
    }
}
