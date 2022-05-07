﻿public abstract class Component : IComponent
{
    public abstract void Initialize(IApp app);

    public abstract void Draw(IApp app, double time);
}
