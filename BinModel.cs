using System.Runtime.CompilerServices;

namespace BinomialPricer {
    public class BinModel {
        private double s0;
        private double u;
        private double d;
        public double r;
        public BinModel(double S0, double U, double D, double R ) {
            s0 = S0;
            u = U;
            d = D;
            r = R;
        }

        public double RiskNeutProb() => (r-d)/(u-d);

        public double S(int n, int i) => s0 * Math.Pow(1 + u, i) * Math.Pow(1 + d, n-i);
    }
}