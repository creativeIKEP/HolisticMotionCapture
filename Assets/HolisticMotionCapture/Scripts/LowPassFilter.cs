using UnityEngine;

namespace HolisticMotionCapture
{
    public class LowPassFilter
    {
        float p1;
        float p2;

        public LowPassFilter(float p1, float p2)
        {
            this.p1 = p1;
            this.p2 = p2;
        }

        public static float Alpha(float cutoff, float t_e)
        {
            float r = 2.0f * 3.141592f * cutoff * t_e;
            return r / (r + 1);
        }

        #region float LPF
        float p_x;
        float p_dx;

        public float Filter(float x, float dt)
        {
            var dx = StepDx(x, dt);
            float cutoff = p2 + p1 * Mathf.Abs(dx);
            var result = Mathf.Lerp(p_x, x, Alpha(cutoff, dt));

            p_x = x;
            p_dx = dx;
            return result;
        }

        public float Filter(float x, float dt, float dx)
        {
            float cutoff = p2 + p1 * Mathf.Abs(dx);
            var result = Mathf.Lerp(p_x, x, Alpha(cutoff, dt));

            p_x = x;
            p_dx = dx;
            return result;
        }

        private float StepDx(float x, float dt)
        {
            var dx = (x - p_x) / dt;
            return Mathf.Lerp(p_dx, dx, Alpha(1, dt));
        }

        #endregion

        #region Vector3 LPF
        Vector3 p_x_v;
        Vector3 p_dx_v;

        public Vector3 Filter(Vector3 x, float dt)
        {
            var dx = StepDx(x, dt);
            float cutoff = p2 + p1 * dx.magnitude;
            var result = Vector3.Lerp(p_x_v, x, Alpha(cutoff, dt));

            p_x_v = x;
            p_dx_v = dx;
            return result;
        }

        public Vector3 Filter(Vector3 x, float dt, Vector3 dx)
        {
            float cutoff = p2 + p1 * dx.magnitude;
            var result = Vector3.Lerp(p_x_v, x, Alpha(cutoff, dt));

            p_x_v = x;
            p_dx_v = dx;
            return result;
        }

        private Vector3 StepDx(Vector3 x, float dt)
        {
            var dx = (x - p_x_v) / dt;
            return Vector3.Lerp(p_dx_v, dx, Alpha(1, dt));
        }
        #endregion
    }
}