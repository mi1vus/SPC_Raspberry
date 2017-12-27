using System.ServiceModel;

//using ServioPump_2._34_Driver;

namespace RemotePump_Driver
{
    //[MessageContract]
    public struct PumpInformation
    {
        /// <summary>
        /// Номер ТРК
        /// </summary>
        public int No
        { get; set; }

        /// <summary>
        /// Состояние ТРК
        /// </summary>
        public PumpState State
        { get; set; }
        public int SelectedProduct
        { get; set; }
        /// <summary>
        /// Информация по рукавам
        /// </summary>
        public ProductInformation[] ProductInformation
        { get; set; }
        private string products()
        {
            var result = "";
            if (ProductInformation != null)
                foreach (var prod in ProductInformation)
                    result += ((result == "")?" ":", ") + ((SelectedProduct == prod.Code)?$"[{prod.Name}]":prod.Name);
            return result;
        }
        public decimal PreselQuantity { get; set; }
        public decimal FillingQuantity { get; set; }
        public decimal FillingAmount { get; set; }
        public decimal FillingPrice { get; set; }
        public string TransactionID { get; set; }
        public override string ToString() => $"ТРК {No}, {State}:{products()}";
    }
    public enum PumpState
    {
        /// <summary>
        /// ТРК доступна
        /// </summary>
        Online,
        /// <summary>
        /// ТРК не доступна
        /// </summary>
        Offline,
        /// <summary>
        /// ТРК занята
        /// </summary>
        Busy,
        /// <summary>
        /// Происходит налив топлива
        /// </summary>
        Filling,
        /// <summary>
        /// На ТРК произошла ошибка
        /// </summary>
        Error,
    }
    public struct ProductInformation
    {
        /// <summary>
        /// Имя продукта
        /// </summary>
        public string Name
        { get; set; }
        /// <summary>
        /// Код продукта
        /// </summary>
        public int Code
        { get; set; }

        /// <summary>
        /// Базовая стоимость продукта
        /// </summary>
        public decimal BasePrice
        {
            get;
            set;
        }
    }

    public struct OrderInfo
    {
        public string TID { get; set; }
        /// <summary>
        /// Номер ТРК
        /// </summary>
        public int PumpNo { get; set; }

        /// <summary>
        /// Номер транзакции
        /// </summary>
        public string OrderRRN { get; set; }

        /// <summary>
        /// Номер карты
        /// </summary>
        public string CardNO { get; set; }

        /// <summary>
        /// Сумма отпущенного топлива
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Цена
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Количество отпущенного топлива
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// Сумма отпущенного топлива
        /// </summary>
        public decimal OverAmount { get; set; }

        /// <summary>
        /// Количество отпущенного топлива
        /// </summary>
        public decimal OverQuantity { get; set; }

        /// <summary>
        /// Код основания
        /// </summary>
        public int PaymentCode { get; set; }
        /// <summary>
        /// Код продукта
        /// </summary>
        public int ProductCode { get; set; }
        /// <summary>
        /// Режим заказа (0 - лит., 1 - руб.)
        /// </summary>
        public int OrderMode { get; set; }

        public bool FillingOver { get; set; }


        public decimal BasePrice { get; set; }


        public string PumpRRN { get; set; }

        /// <summary>
        /// Номер карты
        /// </summary>
        public string DiscontCardNO { get; set; }


        /// <summary>
        /// Номер карты
        /// </summary>
        public string DiscontCardType { get; set; }

        /// <summary>
        /// Предварительно рассчитанная сумма заказа при полном отпуске топлива
        /// </summary>
        public decimal PreCalcedFillingOver_Amount { get; set; }
        /// <summary>
        /// Предварительно рассчитанная скидка заказа при полном отпуске топлива
        /// </summary>
        public decimal PreCalcedFillingOver_Discount { get; set; }
        /// <summary>
        /// Предварительно рассчитанная цена заказа при полном отпуске топлива
        /// </summary>
        public decimal PreCalcedFillingOver_Price { get; set; }

        public override string ToString()
        {
            return
$@"
TID: {TID}
PumpNo: {PumpNo}
OrderRRN: {OrderRRN}
CardNO: {CardNO}
Amount: {Amount}
Price: {Price}
Quantity: {Quantity}
OverAmount: {OverAmount}
OverQuantity: {OverQuantity}
PaymentCode: {PaymentCode}
ProductCode: {ProductCode}
OrderMode: {OrderMode}
FillingOver: {FillingOver}
BasePrice: {BasePrice}
PumpRRN: {PumpRRN}
DiscontCardNO: {DiscontCardNO}
DiscontCardType: {DiscontCardType}
PreCalcedFillingOver_Amount: {PreCalcedFillingOver_Amount}
PreCalcedFillingOver_Discount: {PreCalcedFillingOver_Discount}
PreCalcedFillingOver_Price: {PreCalcedFillingOver_Price}";
        }
    }
    

    [ServiceContract()]
    public interface IRemotePump
    {
        [OperationContract]
        bool SetID(string TID);
        [OperationContract]
        OrderInfo[] GetFillingOvers();
        [OperationContract]
        bool LockPump(int PumpNo);
        [OperationContract]
        bool UnlockPump(int PumpNo);
        [OperationContract]
        string SendCMD(string Cmd, string Data);
        [OperationContract]
        bool SetDose(OrderInfo Order);
        [OperationContract]
        bool CancelDose(OrderInfo Order);
        [OperationContract]
        ProductInformation[] GetProducts();
        [OperationContract]
        PumpInformation GetPumpInformation(int No);
    }

    
}
