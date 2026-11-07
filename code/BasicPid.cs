using System;

namespace SalternPID
{
    // a normal PID controller
    public class BasicPid : IController
    {
        private readonly string _name;
        private readonly double _kp;
        private readonly double _ki;
        private readonly double _kd;
        private readonly bool _filterDerivative;
        private readonly bool _antiWindup;

        private const double Bias = 50;
        private const double SumLimit = 1500;
        private const int WaitSteps = 5;   // the same waiting at the start as in our controller

        private int _stepCount = 0;
        private bool _firstStep = true;
        private double _lastError = 0;
        private double _derivative = 0;
        private double _sum = 0;

        public string Name { get { return _name; } }

        public BasicPid(string name, double kp, double ki, double kd, bool filterDerivative, bool antiWindup)
        {
            _name = name;
            _kp = kp;
            _ki = ki;
            _kd = kd;
            _filterDerivative = filterDerivative;
            _antiWindup = antiWindup;
        }

        // the four controllers for the comparison, with the weights found by trial (grid search)
        public static BasicPid CreateP()
        {
            return new BasicPid("P", 6, 0, 0, false, false);
        }

        public static BasicPid CreatePI()
        {
            return new BasicPid("PI", 3, 0.2, 0, false, false);
        }

        public static BasicPid CreatePID()
        {
            return new BasicPid("PID", 14, 0.02, 100, false, false);
        }

        public static BasicPid CreatePIDPlus()
        {
            return new BasicPid("PID+", 4, 0.2, 10, true, true);
        }

        public double Decide(double measuredLevel, double setPoint, double currentValve)
        {
            // the error (positive = the level is too high)
            double error = measuredLevel - setPoint;
            _stepCount++;

            // the derivative: how much the error changed since the last step (dt = 1)
            double change = 0;
            if (!_firstStep)
                change = error - _lastError;
            _firstStep = false;
            _lastError = error;

            if (_filterDerivative)
                _derivative = (_derivative + change) / 2.0;   // the same smoothing as in our controller
            else
                _derivative = change;

            // the integral (dt = 1), with a limit
            double newSum = _sum + error;
            if (newSum > SumLimit) newSum = SumLimit;
            if (newSum < -SumLimit) newSum = -SumLimit;

            if (_antiWindup)
            {
                double test = Output(error, newSum, _derivative);
                bool pushesMore = (test > 100 && error < 0) || (test < 0 && error > 0);
                if (!pushesMore)
                    _sum = newSum;
            }
            else
            {
                _sum = newSum;
            }

            // at the start the controller only collects data
            if (_stepCount <= WaitSteps)
                return currentValve;

            // the decision
            double wanted = Output(error, _sum, _derivative);
            if (wanted < 0) wanted = 0;
            if (wanted > 100) wanted = 100;
            return wanted;
        }

        // the same formula as in our controller, but with the lines f(x) = x
        private double Output(double error, double sum, double derivative)
        {
            double v = -(_kp * error + _ki * sum + _kd * derivative);
            return Bias + 0.5 * v;
        }
    }
}