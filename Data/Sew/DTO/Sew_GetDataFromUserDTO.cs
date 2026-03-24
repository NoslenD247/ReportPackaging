namespace ReportPackaging.Data.Sew.DTO
{
    public class Sew_GetDataFromUserDTO
    {
        public string ID { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string NameEn { get; set; }
        public string NameKo { get; set; }
        public int CenterId { get; set; }
        public int DeptId { get; set; }
        public int TeamId { get; set; }
        public string CenterName { get; set; }
        public string Country { get; set; }
        public string DepartmentName { get; set; }
            
    }
}
