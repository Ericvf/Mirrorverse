public interface IComponent
{
    void Initialize(IApp app);

    void Draw(IApp app, double time);
}

