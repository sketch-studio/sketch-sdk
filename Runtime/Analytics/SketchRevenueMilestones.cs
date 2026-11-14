using System.Collections.Generic;
using System.Globalization;

namespace Sketch.Sdk
{
    /// <summary>Pure helper: which revenue thresholds were crossed when lifetime revenue moved from before to after.</summary>
    public static class SketchRevenueMilestones
    {
        public static void Crossed(double before, double after, IList<float> milestones, List<float> result)
        {
            result.Clear();
            if (milestones == null || after <= before) return;
            for (int i = 0; i < milestones.Count; i++)
            {
                float m = milestones[i];
                if (m > 0f && before < m && after >= m) result.Add(m);
            }
        }

        /// <summary>Event name for a threshold: 0.05 -> reach_rev_0_05, 1 -> reach_rev_1.</summary>
        public static string EventName(float milestone) =>
            "reach_rev_" + ((decimal)milestone).ToString("0.####", CultureInfo.InvariantCulture).Replace('.', '_');
    }
}
