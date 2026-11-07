using System.Collections.Generic;

namespace SalternPID
{
    // the six test situations; in all of them the level starts at 10 and the valve at 50
    public static class Scenarios
    {
        public static List<Scenario> CreateAll()
        {
            List<Scenario> list = new List<Scenario>();

            // S1: the start of the program
            Scenario s1 = new Scenario();
            s1.Name = "S1";
            s1.Description = "Start of the program (level 10, SP 74)";
            s1.EventStep = 0;
            list.Add(s1);

            // S2: unknown inflow change, u0 goes from 50 to 62
            Scenario s2 = new Scenario();
            s2.Name = "S2";
            s2.Description = "Unknown inflow change (u0 from 50 to 62)";
            s2.NullAfter = 62;
            list.Add(s2);

            // S3: the set point is dragged from 74 to 20
            Scenario s3 = new Scenario();
            s3.Name = "S3";
            s3.Description = "The set point goes from 74 to 20";
            s3.SetPointAfter = 20;
            list.Add(s3);

            // S4: heavy rain (+4 per step, steps 150 to 179), then strong evaporation (-2 per step, steps 250 to 299)
            Scenario s4 = new Scenario();
            s4.Name = "S4";
            s4.Description = "Heavy rain, then strong evaporation";
            s4.RainValue = 4;
            s4.RainFrom = 150;
            s4.RainTo = 180;
            s4.EvaporationValue = -2;
            s4.EvaporationFrom = 250;
            s4.EvaporationTo = 300;
            list.Add(s4);

            // S5: like S2, with noise on the level reading
            Scenario s5 = new Scenario();
            s5.Name = "S5";
            s5.Description = "Like S2, with noise on the level sensor (0.5)";
            s5.NullAfter = 62;
            s5.NoiseSigma = 0.5;
            list.Add(s5);

            // S6: extreme unknown flow change, u0 goes from 50 to 75
            Scenario s6 = new Scenario();
            s6.Name = "S6";
            s6.Description = "Extreme unknown flow change (u0 from 50 to 75)";
            s6.NullAfter = 75;
            list.Add(s6);

            return list;
        }
    }
}