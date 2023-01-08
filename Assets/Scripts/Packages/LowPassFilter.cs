using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LowPassFilter
{
    float p1;
    float p2;
    Vector3 p_x;
    Vector3 p_dx;


    public LowPassFilter(float p1, float p2){
        this.p1 = p1;
        this.p2 = p2;
    }

    public Vector3 Filter(Vector3 x, float dt) {
        var dx = StepDx(x, dt);
        float cutoff = p2 + p1 * dx.magnitude;
        var result = Vector3.Lerp(p_x, x, Alpha(cutoff, dt));

        p_x = x;
        p_dx = dx;
        return result;
    }

    private Vector3 StepDx(Vector3 x, float dt) {
        var dx = (x - p_x) / dt;
        return Vector3.Lerp(p_dx, dx, Alpha(1, dt));
    }
    
    private float Alpha(float cutoff, float t_e) {
        float r = 2.0f * 3.141592f * cutoff * t_e;
        return r / (r + 1);
    }
}
