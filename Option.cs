using System.Security.Cryptography.X509Certificates;
using ScottPlot.AxisRules;

namespace BinomialPricer {
    public static class Option {
        public delegate double Payoff(double z, double K);
        public static double PriceByCRR(BinModel Model, int N, double K, Payoff payoff) {
            double q = Model.RiskNeutProb();
            double[] Price = new double[N+1];
            for (int i = 0; i <=N; i++) {
                Price[i] = payoff(Model.S(N, i), K);
            }
            for (int n = N - 1; n > 0; n--) {
                for (int i =0; i<=n; i++) {
                    Price[i] = (q*Price[i+1] + (1-q)*Price[i])/(1 + Model.r);
                }
            }
            return Price[0];
        }

        public static double CallPayoff(double z, double K) => Math.Max(z-K, 0);

        public static double PutPayoff(double z, double K) => Math.Max(K-z, 0);
    }
}