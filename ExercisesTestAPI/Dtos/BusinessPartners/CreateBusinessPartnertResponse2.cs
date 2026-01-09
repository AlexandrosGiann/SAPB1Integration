namespace ExercisesTestAPI.Dtos.BusinessPartners
{
    public class CreateBusinessPartnertResponse2
    {
        public string CardCode { get; set; } = default!;
        public string CardName { get; set; } = default!;
        public string? BillNotFound { get; set; } = "Καλό είναι να υπάρχει τουλάχιστον ένα bo_billTo στα AdressType του BPAdressess."!;
    }
}
