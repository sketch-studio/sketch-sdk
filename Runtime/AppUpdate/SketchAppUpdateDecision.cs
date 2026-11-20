namespace Sketch.Sdk
{
    public enum SketchAppUpdateDecision
    {
        None = 0,
        /// <summary>Background download; the game shows a small "Restart to update" button when done.</summary>
        Flexible = 1,
        /// <summary>Full-screen blocking update (forced by sketch_force_update_min_version_code).</summary>
        Immediate = 2,
    }
}
