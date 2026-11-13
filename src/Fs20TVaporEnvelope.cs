using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Reduced-order condensation, not a surface-pressure solver. Native part
    // loads provide mean pressure jumps; thin-airfoil theory estimates suction.
    // Temperature and humidity are explicit ISA/RH boundary assumptions.
    internal static class Fs20TVaporEnvelope
    {
        internal struct Atmosphere
        {
            internal float Temperature, Pressure, MixingRatio;
            internal bool Valid;
        }
        internal struct Condensation
        {
            internal float Opacity, Water, Temperature, Pressure;
        }
        internal static Atmosphere Ambient(float density,float altitude,float humidity)
        {
            if(!Finite(density) || !Finite(altitude) || !Finite(humidity) || density<=0f)return default;
            float temperature=288.15f-.0065f*Clamp(altitude,0f,11000f);
            float pressure=density*287.05f*temperature;
            float vapor=Clamp(humidity,0f,1f)*Saturation(temperature);
            return new Atmosphere{Temperature=temperature,Pressure=pressure,
                MixingRatio=.622f*vapor/Math.Max(1f,pressure-vapor),Valid=pressure>1000f};
        }
        // Buck saturation over liquid water, including supercooled droplets.
        // Ice nucleation and persistent high-altitude contrails are not simulated.
        internal static float Saturation(float kelvin)
        {
            float c=Clamp(kelvin-273.15f,-80f,50f);
            return 611.21f*(float)Math.Exp((18.678f-c/234.5f)*c/(257.14f+c));
        }
        internal static float ChordWeight(float fraction)
        {
            float x=Clamp(fraction,.025f,.975f);
            return (float)(2d/Math.PI*Math.Sqrt((1f-x)/x));
        }
        internal static Condensation Evaluate(Atmosphere air,float suction,float opticalDepth)
        {
            if(!air.Valid || !Finite(suction) || !Finite(opticalDepth))return default;
            // Bound this approximation before near-vacuum/unmodelled shock flow.
            float pressure=air.Pressure-Clamp(suction,0f,air.Pressure*.35f);
            float temperature=air.Temperature*(float)Math.Pow(pressure/air.Pressure,2d/7d);
            float saturation=Saturation(temperature);
            float saturatedMix=.622f*saturation/Math.Max(1f,pressure-saturation);
            float dryCondensate=Math.Max(0f,air.MixingRatio-saturatedMix);
            // Saturation adjustment includes latent heat rather than assuming
            // all excess water condenses without warming the expanding parcel.
            float low=0f,high=dryCondensate;
            for(int iteration=0;iteration<6 && high>0f;iteration++){
                float condensed=(low+high)*.5f;
                float warm=temperature+2490f*condensed;
                float es=Saturation(warm);
                float available=Math.Max(0f,air.MixingRatio-.622f*es/Math.Max(1f,pressure-es));
                if(condensed>available)high=condensed;else low=condensed;
            }
            float liquid=(low+high)*.5f;
            temperature+=2490f*liquid;
            float water=pressure/(287.05f*temperature)*liquid;
            // Geometric-optics extinction: 2 um effective droplet radius,
            // 1000 kg/m3 liquid water. OpticalDepth is cloud thickness in metres.
            float opacity=1f-(float)Math.Exp(-750f*water*Math.Max(0f,opticalDepth));
            return new Condensation{Opacity=opacity,Water=water,Temperature=temperature,Pressure=pressure};
        }
        internal static float TipSuction(float lift,float density,float speed,float span,float chord)
        {
            if(!Finite(lift) || density<=0f || speed<=1f || span<=.01f)return 0f;
            // Elliptical semispan loading with a finite Rankine core.
            float circulation=4f*Math.Abs(lift)/((float)Math.PI*density*speed*span);
            float core=Math.Max(.08f,chord*.03f);
            return density*circulation*circulation/(4f*(float)(Math.PI*Math.PI)*core*core);
        }
        internal static float Approach(float previous,float target,float dt) =>
            previous+(target-previous)*(1f-(float)Math.Exp(-Clamp(dt,0f,.1f)/(target>previous?.12f:.22f)));
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value,float min,float max) => Math.Max(min,Math.Min(max,value));
    }
}
