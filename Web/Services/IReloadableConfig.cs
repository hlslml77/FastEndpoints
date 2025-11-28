using System;

namespace Web.Services;

public interface IReloadableConfig
{
    string Name { get; }
    DateTime LastReloadTime { get; }
    void Reload();
    object GetStatus();
}

