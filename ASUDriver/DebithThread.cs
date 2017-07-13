using System.Threading;

namespace SPC_Raspberry
{
    public class DebithThread
    {
        public static long TransID;
        public delegate bool CallbackType(long TransactID);
        public static CallbackType DebitCallback;

        public static void Execute()
        {
            long transID = 0;
	        while (true)
	        {
		        if (transID != TransID)
		        {
			        transID = TransID;
                    DebitCallback?.Invoke(transID);
                }
                Thread.Sleep(100);
            }
            return;
        }
        public static void SetTransID(long transID)
        { 
	        TransID = transID;
        }
    }
}
