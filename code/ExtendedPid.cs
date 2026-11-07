using System;

namespace SalternPID
{
    // the settings of our controller; each "Use..." switch turns one part on or off (experiment E6)
    public class PidSettings
    {
        // the weights of the three parts (found by trial and error)
        public double Kp = 10;
        public double Ki = 0.2;
        public double Kd = 25;

        // the valve position when the error is zero and nothing is accumulated
        public double Bias = 50;

        // how strong the functions fp, fi, fd press the big values (0 = the line f(x) = x)
        public double ShapeP = 0.1;
        public double ShapeI = 0.01;
        public double ShapeD = 0;

        // when the error is smaller than this, the controller sees it as zero
        public double DeadBand = 0.2;

        // the controller waits some steps at the start to collect data (the second derivative needs at least 3 values of the error)
        public int WaitSteps = 5;

        // how many steps in a row the error must go away from zero to cancel the derivative
        public int NotConvergeSteps = 5;

        // limit of the integral
        public double SumLimit = 1500;

        // switches
        public bool UseSecondDerivative = true;
        public bool UseSmoothing = true;
        public bool UseDeadBand = true;
        public bool UseIntegralReset = true;
        public bool UseCancelDerivative = true;
        public bool UseNonlinearP = true;
        public bool UseNonlinearI = true;
        public bool UseNonlinearD = true;
        public bool UseTrapezoid = false;   // false = right rectangle rule, true = trapezoid rule

        public PidSettings Copy()
        {
            return (PidSettings)MemberwiseClone();
        }
    }

    // our modified PID controller
    // it does not know the inflow or the outflow; it uses only the error e = PV - SP and how the error changes from one step to the next (dt = 1)
    public class ExtendedPid : IController
    {
        private readonly PidSettings _s;

        // memory of the controller
        private int _stepCount = 0;
        private bool _firstStep = true;
        private double _lastError = 0;
        private double _smoothError = 0;          // es
        private double _smoothRate = 0;           // d1s, first derivative (smoothed)
        private double _smoothAcceleration = 0;   // d2s, second derivative (smoothed)
        private double _sum = 0;                  // S, the integral (accumulated error)
        private int _notConvergeCount = 0;

        // numbers for the screen and for the experiments
        public int CancelCount { get; private set; }
        public double Error { get { return _lastError; } }
        public double SmoothError { get { return _smoothError; } }
        public double Rate { get { return _smoothRate; } }
        public double Acceleration { get { return _smoothAcceleration; } }
        public double Sum { get { return _sum; } }

        public string Name { get { return "Extended PID"; } }
        public PidSettings Settings { get { return _s; } }

        public ExtendedPid(PidSettings settings)
        {
            _s = settings;
        }

        public double Decide(double measuredLevel, double setPoint, double currentValve)
        {
            // the error (positive = the level is too high)
            double error = measuredLevel - setPoint;
            if (_s.UseDeadBand && Math.Abs(error) < _s.DeadBand)
                error = 0;

            _stepCount++;

            // smoothing and the first and second derivative of the error
            double newRate = 0;
            double newAcceleration = 0;
            if (_firstStep)
            {
                _smoothError = error;
                _lastError = error;
                _firstStep = false;
            }
            else
            {
                newRate = Math.Round(error - _smoothError, 5);
                newAcceleration = newRate - _smoothRate;
            }

            if (_s.UseSmoothing)
            {
                _smoothError = (_smoothError + error) / 2.0;
                _smoothRate = Math.Round((_smoothRate + newRate) / 2.0, 5);
                _smoothAcceleration = Math.Round((_smoothAcceleration + newAcceleration) / 2.0, 5);
            }
            else
            {
                _smoothError = error;
                _smoothRate = newRate;
                _smoothAcceleration = newAcceleration;
            }

            // the integral, with dt = 1
            double toAdd = error;
            if (_s.UseTrapezoid)
                toAdd = (error + _lastError) / 2.0;
            _sum = _sum + toAdd;
            _lastError = error;

            if (_sum > _s.SumLimit) _sum = _s.SumLimit;
            if (_sum < -_s.SumLimit) _sum = -_s.SumLimit;

            // when the error changes sign, the old accumulated error is cleared
            if (_s.UseIntegralReset)
            {
                if (_sum > 0 && error < 0) _sum = 0;
                if (_sum < 0 && error > 0) _sum = 0;
            }

            // does the process converge? (the error must not go away from zero)
            bool goesAway = (error > 0 && _smoothRate > 0) || (error < 0 && _smoothRate < 0);
            if (goesAway) _notConvergeCount++;
            else _notConvergeCount = 0;

            // at the start the controller only collects data
            if (_stepCount <= _s.WaitSteps)
                return currentValve;

            // the decision
            double derivative = _s.UseSecondDerivative
                ? (3 * _smoothRate + 2 * _smoothAcceleration) / 5.0   // 3/5 rate and 2/5 acceleration
                : _smoothRate;

            double pPart = _s.Kp * Shape(_smoothError, _s.ShapeP, _s.UseNonlinearP);
            double iPart = _s.Ki * Shape(_sum, _s.ShapeI, _s.UseNonlinearI);
            double dPart = _s.Kd * Shape(derivative, _s.ShapeD, _s.UseNonlinearD);

            double v = -(pPart + iPart + dPart);
            double wanted = _s.Bias + 0.5 * v;
            if (wanted < 0) wanted = 0;
            if (wanted > 100) wanted = 100;

            // if the process does not converge, we cancel the derivative component
            if (_s.UseCancelDerivative && _notConvergeCount >= _s.NotConvergeSteps)
            {
                _smoothRate = 0;
                _smoothAcceleration = 0;
                _smoothError = error;
                _notConvergeCount = 0;
                CancelCount++;
            }

            return wanted;
        }

        // the nonlinear function (fp, fi or fd)
        // for small x it is almost x; for big x it grows slowly (like a logarithm), so a big error counts less than in the line f(x) = x
        private double Shape(double x, double k, bool useNonlinear)
        {
            if (!useNonlinear || k <= 0)
                return x;

            return Math.Sign(x) * Math.Log(1 + k * Math.Abs(x)) / k;
        }
    }
}