namespace ELIBAPI.Core.DTOs.Request;

// ==================== AACR2_FIELD ====================
public class Aacr2FieldRequest
{
    public int?    Config_Id  { get; set; }
    public string? Field      { get; set; }
    public string? Starttp    { get; set; }
    public string? Stoptp     { get; set; }
    public int?    Fieldindex { get; set; }
}
public class Aacr2FieldSearchRequest : SearchRequest { }

// ==================== AACR2_SUBFIELD ====================
public class Aacr2SubfieldRequest
{
    public int?    Config_Id     { get; set; }
    public string? Field         { get; set; }
    public string? Subfield      { get; set; }
    public string? Starttp       { get; set; }
    public string? Stoptp        { get; set; }
    public string? Nexttp        { get; set; }
    public int?    Subfieldindex { get; set; }
}
public class Aacr2SubfieldSearchRequest : SearchRequest { }

// ==================== AB_DELIVERER ====================
public class AbDelivererRequest
{
    public long?     Code             { get; set; }
    public string?   ReceiptName      { get; set; }
    public string?   ReceiptAddress   { get; set; }
    public string?   DelivererName    { get; set; }
    public string?   DelivererAddress { get; set; }
    public DateTime? DelivererDate    { get; set; }
    public DateTime? ReceiptDate      { get; set; }
    public int?      UserIddeliverer  { get; set; }
    public int?      UserIdReceipt    { get; set; }
    public int?      Status           { get; set; }
    public int?      Store_Id         { get; set; }
    public int?      Receipt_Id       { get; set; }
    public string?   Note             { get; set; }
    public int?      Sign             { get; set; }
}
public class AbDelivererSearchRequest : SearchRequest
{
    public long?     CodeFrom          { get; set; }
    public long?     CodeTo            { get; set; }
    public string?   DelivererName     { get; set; }
    public string?   ReceiptName       { get; set; }
    public long?     CreatedBy         { get; set; }
    public DateTime? DelivererDateFrom { get; set; }
    public DateTime? DelivererDateTo   { get; set; }
    public int?      Sign              { get; set; }
}

// ==================== AB_DELIVERER_DETAIL ====================
public class AbDelivererDetailRequest
{
    public long? Deliverer_Id { get; set; }
    public long? BarcodeId    { get; set; }
    public int?  Store_Id     { get; set; }
}
public class AbDelivererDetailSearchRequest : SearchRequest { }

// ==================== AB_DELIVERER_STATUS ====================
public class AbDelivererStatusRequest { public string? Name { get; set; } }
public class AbDelivererStatusSearchRequest : SearchRequest { }

// ==================== AB_MOVE ====================
public class AbMoveRequest
{
    public long?     Code             { get; set; }
    public string?   ReceiptName      { get; set; }
    public string?   ReceiptAddress   { get; set; }
    public string?   DelivererName    { get; set; }
    public string?   DelivererAddress { get; set; }
    public DateTime? DelivererDate    { get; set; }
    public DateTime? ReceiptDate      { get; set; }
    public int?      UserIddeliverer  { get; set; }
    public int?      UserIdReceipt    { get; set; }
    public int?      Status           { get; set; }
    public int?      StoreDeliver_Id  { get; set; }
    public int?      StoreReceipt_Id  { get; set; }
    public string?   Note             { get; set; }
}
public class AbMoveSearchRequest : SearchRequest { }

// ==================== AB_MOVE_DETAIL ====================
public class AbMoveDetailRequest
{
    public long? Move_Id   { get; set; }
    public long? BarcodeId { get; set; }
    public int?  Store_Id  { get; set; }
}
public class AbMoveDetailSearchRequest : SearchRequest { }

// ==================== AB_ORDER ====================
public class AbOrderRequest
{
    public DateTime? Date_Order      { get; set; }
    public DateTime? Duedate         { get; set; }
    public long?     Source_Id       { get; set; }
    public int?      Supplier_Id     { get; set; }
    public int?      BUDGET_ID       { get; set; }
    public long?     CREATED_BY      { get; set; }
    public string?   Order_Name      { get; set; }
    public long?     Code            { get; set; }
    public int?      Status          { get; set; }
    public string?   Note            { get; set; }
    public DateTime? CreatedDate     { get; set; }
    public int?      PaymentMethodId { get; set; }
    public long?     FundId          { get; set; }
    public int?      Payment_status  { get; set; }
}
public class AbOrderSearchRequest : SearchRequest { }

// ==================== AB_ORDER_DETAIL ====================
public class AbOrderDetailRequest
{
    public long?    Order_Id     { get; set; }
    public long?    Bibid        { get; set; }
    public int?     Amount       { get; set; }
    public double?  Price        { get; set; }
    public string?  CURRENCY     { get; set; }
    public double?  Rate         { get; set; }
    public string?  Cancel_Reson { get; set; }
}
public class AbOrderDetailSearchRequest : SearchRequest { }

// ==================== AB_RECEIPT ====================
public class AbReceiptRequest
{
    public DateTime? Receipt_Date    { get; set; }
    public long?     Source_Id       { get; set; }
    public long?     Supplier_Id     { get; set; }
    public long?     BUDGET_ID       { get; set; }
    public long?     CREATED_BY      { get; set; }
    public string?   Receipt_Name    { get; set; }
    public long?     Code            { get; set; }
    public int?      Status          { get; set; }
    public int?      Payment_Status  { get; set; }
    public string?   Note            { get; set; }
    public long?     Store_Id        { get; set; }
    public DateTime? CreatedDate     { get; set; }
    public int?      PaymentMethodId { get; set; }
    public long?     FundId          { get; set; }
}
public class AbReceiptSearchRequest : SearchRequest
{
    public long?     CodeFrom        { get; set; }
    public long?     CodeTo          { get; set; }
    public string?   ReceiptName     { get; set; }
    public long?     CreatedBy       { get; set; }
    public long?     SupplierId      { get; set; }
    public DateTime? ReceiptDateFrom { get; set; }
    public DateTime? ReceiptDateTo   { get; set; }
    public DateTime? CreatedDateFrom { get; set; }
    public DateTime? CreatedDateTo   { get; set; }
    public long?     SourceId        { get; set; }
    public long?     FundId          { get; set; }
}

// ==================== AB_RECEIPT_DETAIL ====================
public class AbReceiptDetailRequest
{
    public long?     Receipt_Id { get; set; }
    public long?     Bibid      { get; set; }
    public int?      Amount     { get; set; }
    public double?   Price      { get; set; }
    public string?   CURRENCY   { get; set; }
    public double?   Rate       { get; set; }
    public long?     Order_Id   { get; set; }
    public long?     OrderDetailId { get; set; }
    public DateTime? Submited   { get; set; }
}
public class AbReceiptDetailSearchRequest : SearchRequest { }

// ==================== AB_SOURCE ====================
public class AbSourceRequest { public string? Name { get; set; } }
public class AbSourceSearchRequest : SearchRequest { }

// ==================== AH_RECEIPT ====================
public class AhReceiptRequest
{
    public DateTime? Receipt_Date    { get; set; }
    public long?     Source_Id       { get; set; }
    public long?     Supplier_Id     { get; set; }
    public long?     BUDGET_ID       { get; set; }
    public long?     CREATED_BY      { get; set; }
    public string?   Receipt_Name    { get; set; }
    public long?     Code            { get; set; }
    public int?      Status          { get; set; }
    public int?      Payment_Status  { get; set; }
    public string?   Note            { get; set; }
    public long?     Store_Id        { get; set; }
    public DateTime? CreatedDate     { get; set; }
    public int?      PaymentMethodId { get; set; }
    public long?     FundId          { get; set; }
}
public class AhReceiptSearchRequest : SearchRequest { }

// ==================== BARCODE ====================
public class BarcodeRequest
{
    public string? BarcodeValue  { get; set; }
    public int?    BarcodeNumber { get; set; }
    public int?    Store         { get; set; }
    public long?   BibId         { get; set; }
    public string? Status        { get; set; }
    public long?   Receipt_Id    { get; set; }
}
public class BarcodeSearchRequest : SearchRequest
{
    public long? BibId   { get; set; }
    public int?  StoreId { get; set; }
}

// ==================== BARCODE_STATUS ====================
public class BarcodeStatusRequest
{
    public string? Id            { get; set; }
    public string? CommentStatus { get; set; }
}
public class BarcodeStatusSearchRequest : SearchRequest { }

// ==================== BIB ====================
public class BibRequest
{
    public long?    Mfn              { get; set; }
    public string?  Status           { get; set; }
    public long?    Bib_worksheet_id { get; set; }
    public long?    Bib_type_id      { get; set; }
    public string?  MARC_STATUS      { get; set; }
    public string?  Url              { get; set; }
    public string?  Images           { get; set; }
    public long?    EbookId          { get; set; }
    public long?    DocNum           { get; set; }
    public long?    CollectionId     { get; set; }
}
public class BibSearchRequest : SearchRequest
{
    public long?   Bib_type_id { get; set; }
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? PublishYear { get; set; }
    public long?   MfnFrom     { get; set; }
    public long?   MfnTo       { get; set; }
}
public class BibBulkMoveCollectionRequest
{
    public List<long> BibIds       { get; set; } = [];
    public long        CollectionId { get; set; }
}

// ==================== BOOK DOC SEARCH (module tìm kiếm tài liệu in ấn) ====================
// Không kế thừa SearchRequest vì Status ở đây là string (Barcode.Status/Bib.Status), khác kiểu int? của SearchRequest.Status
public class BookDocSearchRequest
{
    public long?   MfnFrom     { get; set; }
    public long?   MfnTo       { get; set; }
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? PublishYear { get; set; }
    public string? Keyword     { get; set; }
    public string? Summary     { get; set; }
    public string? CallNumber  { get; set; }
    public long?   DocTypeId   { get; set; }
    public string? Status      { get; set; }
    public int?    StoreId     { get; set; }
    public Guid?   TenantId    { get; set; }
    public int     PageIndex   { get; set; } = 1;
    public int     PageSize    { get; set; } = 10;
}

// ==================== BIB_TYPE ====================
public class BibTypeRequest
{
    public string? Name             { get; set; }
    public string? Code             { get; set; }
    public string? Bib_Level        { get; set; }
    public string? Record_Type_Code { get; set; }
    public string? Materal_Type     { get; set; }
    public string? Type             { get; set; }
}
public class BibTypeSearchRequest : SearchRequest { }

// ==================== BIB_WORKSHEET ====================
public class BibWorksheetRequest
{
    public string? Name        { get; set; }
    public string? Usmarc      { get; set; }
    public int?    Bib_Type_Id { get; set; }
}
public class BibWorksheetSearchRequest : SearchRequest
{
    public int? BibTypeId { get; set; }
}

// ==================== BIB_DATA ====================
public class BibDataRequest
{
    public long?   BibId      { get; set; }
    public string? Field      { get; set; }
    public string? SubField   { get; set; }
    public string? Data       { get; set; }
    public string? Fk         { get; set; }
    public string? L1         { get; set; }
    public string? L2         { get; set; }
    public string? Title      { get; set; }
    public string? DataUnsign { get; set; }
}
public class BibDataSearchRequest : SearchRequest
{
    public long? BibId { get; set; }
}

// ==================== BIB_DATA_ORDER ====================
public class BibDataOrderRequest
{
    public long?   BibId      { get; set; }
    public string? Field      { get; set; }
    public string? SubField   { get; set; }
    public string? Data       { get; set; }
    public string? Fk         { get; set; }
    public string? L1         { get; set; }
    public string? L2         { get; set; }
    public string? Title      { get; set; }
    public string? DataUnsign { get; set; }
}
public class BibDataOrderSearchRequest : SearchRequest
{
    public long? BibId { get; set; }
}

// ==================== BIB_ORDER ====================
public class BibOrderRequest
{
    public long?    Mfn              { get; set; }
    public string?  Status           { get; set; }
    public long?    Bib_Worksheet_Id { get; set; }
    public long?    Bib_Type_Id      { get; set; }
    public string?  MARC_STATUS      { get; set; }
    public string?  Url              { get; set; }
    public string?  Images           { get; set; }
}
public class BibOrderSearchRequest : SearchRequest { }

// ==================== BIB_XML ====================
public class BibXmlRequest
{
    public string? Title            { get; set; }
    public string? Author           { get; set; }
    public string? Publisher        { get; set; }
    public string? PublishDate      { get; set; }
    public string? Price            { get; set; }
    public string? Page             { get; set; }
    public int?    Bib_Worksheet_Id { get; set; }
    public int?    Bib_Type_Id      { get; set; }
    public string? Isbd             { get; set; }
    public string? Accr2            { get; set; }
    public string? Keyword          { get; set; }
    public int?    UserId           { get; set; }
    public string? DDC              { get; set; }
}
public class BibXmlSearchRequest : SearchRequest { }

// ==================== BIB_XML_ORDER ====================
public class BibXmlOrderRequest
{
    public string? Title            { get; set; }
    public string? Author           { get; set; }
    public string? Publisher        { get; set; }
    public string? PublishDate      { get; set; }
    public string? Price            { get; set; }
    public string? Page             { get; set; }
    public int?    Bib_Worksheet_Id { get; set; }
    public int?    Bib_Type_Id      { get; set; }
    public string? Isbd             { get; set; }
    public string? Accr2            { get; set; }
    public string? Keyword          { get; set; }
    public int?    UserId           { get; set; }
    public string? DDC              { get; set; }
}
public class BibXmlOrderSearchRequest : SearchRequest { }

// ==================== BOOK_GROUP ====================
public class BookGroupRequest
{
    public long? Bib_Id1 { get; set; }
    public long? Bib_Id2 { get; set; }
    public int?  Link    { get; set; }
    public int?  Mode    { get; set; }
    public long? Fieldid { get; set; }
}
public class BookGroupSearchRequest : SearchRequest { }

// ==================== BOOK_GROUP_DETAIL ====================
public class BookGroupDetailRequest
{
    public long? Book_Group_Id { get; set; }
    public long? Bibid         { get; set; }
}
public class BookGroupDetailSearchRequest : SearchRequest { }

// ==================== BOOK_IN ====================
public class BookInRequest
{
    public long?     ReaderId   { get; set; }
    public string?   Barcode    { get; set; }
    public DateTime? BorrowDate { get; set; }
    public DateTime? DueDate    { get; set; }
    public DateTime? ReturnDate { get; set; }
    public int?      UserId     { get; set; }
    public string?   Note       { get; set; }
    public long?     BookOutId  { get; set; }
    public int?      Renew      { get; set; }
    public long?     CircPlace  { get; set; }
    public long?     StoreId    { get; set; }
    public double?   FineValue  { get; set; }
}
public class BookInSearchRequest : SearchRequest
{
    public long? ReaderId { get; set; }
    public long? StoreId  { get; set; }
}

// ==================== BOOK_OUT ====================
public class BookOutRequest
{
    public long?     ReaderId   { get; set; }
    public DateTime? BorrowDate { get; set; }
    public DateTime? DueDate    { get; set; }
    public long?     UserId     { get; set; }
    public int?      Renew      { get; set; }
    public string?   Note       { get; set; }
    public int?      CircPlace  { get; set; }
    public string?   Barcode    { get; set; }
    public long?     RequestId  { get; set; }
    public string?   Status     { get; set; }
    public long?     Reg_Seq_Id { get; set; }
    public long?     Store      { get; set; }
    public double?   FineValue  { get; set; }
}
public class BookOutSearchRequest : SearchRequest
{
    public long? ReaderId { get; set; }
}

// ==================== BOOK_REQUEST ====================
public class BookRequestRequest
{
    public long?     ReaderId   { get; set; }
    public DateTime? BorrowDate { get; set; }
    public DateTime? DueDate    { get; set; }
    public string?   Note       { get; set; }
    public int?      CircPlace  { get; set; }
    public string?   Barcode    { get; set; }
    public string?   Status     { get; set; }
    public long?     UserId     { get; set; }
    public double?   FineValue  { get; set; }
}
public class BookRequestSearchRequest : SearchRequest
{
    public long? ReaderId { get; set; }
}

// ==================== BUDGET ====================
public class BudgetRequest
{
    public string?   Name      { get; set; }
    public double?   Blance    { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime   { get; set; }
    public string?   Note      { get; set; }
    public int?      Status    { get; set; }
}
public class BudgetSearchRequest : SearchRequest { }

// ==================== C_FINE ====================
public class CFineRequest
{
    public long?     ReaderId       { get; set; }
    public DateTime? FineDate       { get; set; }
    public string?   Fine_type_id   { get; set; }
    public DateTime? Returndate     { get; set; }
    public string?   Note           { get; set; }
    public double?   Value          { get; set; }
    public int?      Fine_method_id { get; set; }
    public long?     Borrow_Id      { get; set; }
    public DateTime? BorrowDate     { get; set; }
    public int?      Created_by     { get; set; }
    public string?   Barcode        { get; set; }
    public int?      Lanphat        { get; set; }
    public long?     Bibid          { get; set; }
    public long?     TicketId       { get; set; }
}
public class CFineSearchRequest : SearchRequest
{
    public long? ReaderId { get; set; }
}

// ==================== C_FINE_TICKET ====================
public class CFineTicketRequest
{
    public string?   Code           { get; set; }
    public long?     ReaderId       { get; set; }
    public DateTime? FineDate       { get; set; }
    public int?      Status         { get; set; }
    public int?      Lanphat        { get; set; }
    public double?   DiscountAmount { get; set; }
    public double?   PaidAmount     { get; set; }
    public double?   TotalAmount    { get; set; }
    public int?      OwesDocument   { get; set; }
    public long?     FineTypeId     { get; set; }
    public int?      FineMethodId   { get; set; }
    public string?   Note           { get; set; }
}
public class CFineTicketSearchRequest : SearchRequest
{
    public long?     ReaderId     { get; set; }
    public string?   Code         { get; set; }
    public string?   CardNo       { get; set; }
    public string?   ReaderName   { get; set; }
    public int?      StatusFilter { get; set; }
    public int?      DebtStatus   { get; set; }  // 1=Còn nợ, 2=Đã thanh toán đủ
    public int?      FineMethodId { get; set; }
    public long?     CreatedRowBy { get; set; }
    public DateTime? FineDateFrom { get; set; }
    public DateTime? FineDateTo   { get; set; }
}

// ==================== C_FINE_METHOD ====================
public class CFineMethodRequest { public string? Name { get; set; } }
public class CFineMethodSearchRequest : SearchRequest { }

// ==================== C_FINE_TYPE ====================
public class CFineTypeRequest
{
    public string? Code           { get; set; }
    public string? Name           { get; set; }
    public string? Status_Reg_Id  { get; set; }
}
public class CFineTypeSearchRequest : SearchRequest { }

// ==================== C_PHOTO ====================
public class CPhotoRequest
{
    public string?   CardNo      { get; set; }   // Client gửi số thẻ — server tự resolve ra Reader_Id
    public string?   Barcode     { get; set; }   // Số ĐKCB
    public long?     Reader_Id   { get; set; }   // Server tự gán từ CardNo
    public int?      Frompage    { get; set; }
    public int?      ToPage      { get; set; }
    public double?   Price       { get; set; }
    public DateTime? PhotoDate   { get; set; }
    public int?      Copy        { get; set; }
    public double?   TotalAmount { get; set; }   // Server tự tính, ghi đè giá trị client gửi
    public int?      IsPaid      { get; set; }
    public int?      Status      { get; set; }
}
public class CPhotoSearchRequest : SearchRequest
{
    public string?   CardNo        { get; set; }
    public string?   LastName      { get; set; }   // Họ
    public string?   FirstName     { get; set; }   // Tên
    public string?   BibTitle      { get; set; }   // Nhan đề
    public DateTime? PhotoDateFrom { get; set; }
    public DateTime? PhotoDateTo   { get; set; }
    public int?      IsPaid        { get; set; }
}

// ==================== C_QUEUE_STATUS ====================
public class CQueueStatusRequest
{
    public int?    Type   { get; set; }
    public string? Status { get; set; }
}
public class CQueueStatusSearchRequest : SearchRequest { }

// ==================== C_RENEW ====================
public class CRenewRequest
{
    public long?     Borrow_id          { get; set; }
    public long?     Reg_Seq_Id         { get; set; }
    public string?   Reg_Id             { get; set; }
    public int?      Circ_Place_Id      { get; set; }
    public int?      Status_Id          { get; set; }
    public DateTime? Renew_Date         { get; set; }
    public DateTime? Duedate_Request    { get; set; }
    public int?      Active             { get; set; }
    public int?      Status_Email       { get; set; }
    public int?      Renew_Date_Num     { get; set; }
    public DateTime? Borrow_Date        { get; set; }
    public DateTime? Duedate            { get; set; }
    public long?     Reader_Id          { get; set; }
}
public class CRenewSearchRequest : SearchRequest { }

// ==================== C_RENEW_DATA ====================
public class CRenewDataRequest
{
    public long?     Borrow_id    { get; set; }
    public long?     Reg_Seq_Id   { get; set; }
    public string?   Reg_Id       { get; set; }
    public DateTime? Renew_Date   { get; set; }
    public DateTime? Due_Date_Old { get; set; }
    public DateTime? Due_Date_New { get; set; }
    public DateTime? Borrow_Date  { get; set; }
    public long?     Reader_Id    { get; set; }
    public int?      Created_By   { get; set; }
}
public class CRenewDataSearchRequest : SearchRequest { }

// ==================== CABINET ====================
public class CabinetRequest
{
    public string? Name        { get; set; }
    public string? Code        { get; set; }
    public long?   CircPlaceId { get; set; }
    public int?    Status      { get; set; }
    public string? Note        { get; set; }
    public double? PositionX   { get; set; }
    public double? PositionY   { get; set; }
    public int?    Rows        { get; set; }
    public int?    Cols        { get; set; }
}
public class CabinetSearchRequest : SearchRequest { }

// ==================== CABINET_COMPARTMENT ====================
public class CabinetCompartmentRequest
{
    public long?   CabinetId { get; set; }
    public int?    RowIndex  { get; set; }
    public int?    ColIndex  { get; set; }
    public string? Code      { get; set; }
    public string? Name      { get; set; }
    public int?    Status    { get; set; }
    public string? Note      { get; set; }
}
public class CabinetCompartmentSearchRequest : SearchRequest { public long? CabinetId { get; set; } }

// ==================== CHECK_IN ====================
public class CheckInRequest
{
    public long?     Readerid    { get; set; }
    public long?     UserId      { get; set; }
    public DateTime? CheckInTime { get; set; }
    public long?     StoreId     { get; set; }
}
public class CheckInSearchRequest : SearchRequest { }

// ==================== CHECK_OUT ====================
public class CheckOutRequest
{
    public long?     ReaderId     { get; set; }
    public long?     UserId       { get; set; }
    public DateTime? CheckInTime  { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public long?     StoreId      { get; set; }
    public long?     CheckInId    { get; set; }
}
public class CheckOutSearchRequest : SearchRequest { }

// ==================== CIRC_PLACE ====================
public class CircPlaceRequest
{
    public string? Name                     { get; set; }
    public string? Work_Session              { get; set; }
    public int?    Cir_Type                 { get; set; }
    public int?    Cir_Work_Follow          { get; set; }
    public int?    Access_Request_Validtime { get; set; }
    public int?    Limit_Book               { get; set; }
    public int?    Vitual                   { get; set; }
    public string? Code                     { get; set; }
    public int?    AutoAccept               { get; set; }
}
public class CircPlaceSearchRequest : SearchRequest { }

// ==================== CIRC_PLACE — mapping Kho / Loại bạn đọc ====================
public class CircPlaceReplaceStoreMappingRequest
{
    public Guid       CircPlacePublicId { get; set; }
    public List<long> StoreIds          { get; set; } = new();
}
public class CircPlaceReplaceReaderTypeMappingRequest
{
    public Guid       CircPlacePublicId { get; set; }
    public List<long> ReaderTypeIds     { get; set; } = new();
}
public class CircPlaceStoreRequest
{
    public int  CircPlaceId { get; set; }
    public long StoreId     { get; set; }
}
public class CircPlaceStoreSearchRequest : SearchRequest
{
    public int? CircPlaceId { get; set; }
}
public class CircPlaceReaderTypeRequest
{
    public int  CircPlaceId  { get; set; }
    public long ReaderTypeId { get; set; }
}
public class CircPlaceReaderTypeSearchRequest : SearchRequest
{
    public int? CircPlaceId { get; set; }
}

// ==================== DOC_GROUP ====================
public class DocGroupRequest
{
    public string? Name { get; set; }
}
public class DocGroupSearchRequest : SearchRequest { }

// ==================== POLICY_CIRC_DOC_GROUP ====================
public class PolicyCircDocGroupRequest
{
    public int?  PolicyCircId       { get; set; }
    // long: trỏ BibType.Id (đổi từ DocGroup.Id kiểu int — DocGroup là bảng thừa, không dùng ở đâu khác).
    public long? DocGroupId         { get; set; }
    public int? NumberOfBook       { get; set; }
    public int? NumberOfRequest    { get; set; }
    public int? NumberOfRenewQty   { get; set; }
    public int? NumberOfBookAccept { get; set; }
}
public class PolicyCircDocGroupSearchRequest : SearchRequest
{
    public int? PolicyCircId { get; set; }
}

// ==================== POLICY_CIRC_FINE ====================
public class PolicyCircFineRequest
{
    public int?    PolicyCircId { get; set; }
    public long?   FineTypeId   { get; set; }
    public int?    FineMethodId { get; set; }
    public int?    HoldCardDays { get; set; }
    public double? FineAmount   { get; set; }
}
public class PolicyCircFineSearchRequest : SearchRequest
{
    public int? PolicyCircId { get; set; }
}

// ==================== CONFIG_RECEIPTION ====================
public class ConfigReceiptionRequest
{
    public int? CheckType       { get; set; }
    public int? AutoCheck       { get; set; }
    public int? CircPlaceId     { get; set; }
    public int? RequirePassword { get; set; }
}
public class ConfigReceiptionSearchRequest : SearchRequest
{
    public long? UserId { get; set; }
}

// ==================== CONFIG_AACR2 ====================
public class ConfigAacr2Request
{
    public int? Bib_Type_Id  { get; set; }
    public int? Used_Type_Id { get; set; }
}
public class ConfigAacr2SearchRequest : SearchRequest { }

// ==================== CONFIG_ISBD ====================
public class ConfigIsbdRequest { public int? Bib_Type_Id { get; set; } }
public class ConfigIsbdSearchRequest : SearchRequest { }

// ==================== COUNTRIES ====================
public class CountriesRequest
{
    public string? Code          { get; set; }
    public string? VnDescription { get; set; }
    public string? Description   { get; set; }
    public string? Region        { get; set; }
}
public class CountriesSearchRequest : SearchRequest { }

// ==================== D_BIB_STATUS ====================
public class DBibStatusRequest
{
    public string? Id        { get; set; }
    public string? Name      { get; set; }
    public string? Opac_Name { get; set; }
    public string? English   { get; set; }
}
public class DBibStatusSearchRequest : SearchRequest { }

// ==================== D_KEY ====================
public class DKeyRequest
{
    public string? Keyword     { get; set; }
    public string? Description { get; set; }
}
public class DKeySearchRequest : SearchRequest { }

// ==================== D_PUBLISHER ====================
public class DPublisherRequest
{
    public string? Publisher { get; set; }
    public string? Place     { get; set; }
}
public class DPublisherSearchRequest : SearchRequest { }

// ==================== DFIX_FIELD ====================
public class DFixFieldRequest
{
    public string? VnDescription    { get; set; }
    public string? Description      { get; set; }
    public string? Field            { get; set; }
    public long?   Marc_Type_Id     { get; set; }
    public long?   Material_Type_Id { get; set; }
}
public class DFixFieldSearchRequest : SearchRequest { }

// ==================== DFIX_FIELD_POST ====================
public class DFixFieldPostRequest
{
    public int?    PostNumber   { get; set; }
    public int?    PostLengh    { get; set; }
    public long?   FixFieldId   { get; set; }
    public string? VnDesciption { get; set; }
    public string? Description  { get; set; }
}
public class DFixFieldPostSearchRequest : SearchRequest { }

// ==================== DFIX_FIELD_VALUE ====================
public class DFixFieldValueRequest
{
    public long?   FixFieldPostId { get; set; }
    public string? Value          { get; set; }
    public string? VnDescription  { get; set; }
    public string? Description    { get; set; }
}
public class DFixFieldValueSearchRequest : SearchRequest { }

// ==================== DIC_AUTHOR ====================
public class DicAuthorRequest
{
    public string? DisplayName { get; set; }
    public string? AccessName  { get; set; }
    public string? UnsignName  { get; set; }
    public string? PortalId    { get; set; }
    public string? Language    { get; set; }
}
public class DicAuthorSearchRequest : SearchRequest { }

// ==================== DIC_CLASS ====================
public class DicClassRequest
{
    public long?   ParentId      { get; set; }
    public string? Type          { get; set; }
    public string? Code          { get; set; }
    public string? VnDescription { get; set; }
    public string? Description   { get; set; }
}
public class DicClassSearchRequest : SearchRequest { }

// ==================== DIC_KEYWORD ====================
public class DicKeywordRequest
{
    public string? DisplayName { get; set; }
    public string? AccessName  { get; set; }
    public string? UnsignName  { get; set; }
    public string? PortalId    { get; set; }
    public string? Language    { get; set; }
}
public class DicKeywordSearchRequest : SearchRequest { }

// ==================== DIC_PUBLISHER ====================
public class DicPublisherRequest
{
    public string? DisplayName { get; set; }
    public string? AccessName  { get; set; }
    public string? UnsignName  { get; set; }
    public string? PortalId    { get; set; }
    public string? Language    { get; set; }
}
public class DicPublisherSearchRequest : SearchRequest { }

// ==================== FIXED_FIELD_VALUE ====================
public class FixedFieldValueRequest
{
    public long?   Bibid { get; set; }
    public string? Field { get; set; }
    public string? Value { get; set; }
}
public class FixedFieldValueSearchRequest : SearchRequest { }

// ==================== FREQUENCY_MAGAZINE ====================
public class FrequencyMagazineRequest
{
    public string? Name         { get; set; }
    public long?   DV           { get; set; }
    public int?    SoTrenDV     { get; set; }
    public int?    DVTrenSo     { get; set; }
    public string? NgayPhatHanh { get; set; }
    public int?    Order        { get; set; }
}
public class FrequencyMagazineSearchRequest : SearchRequest { }

// ==================== FUND ====================
public class FundRequest
{
    public string? Name     { get; set; }
    public string? Manager  { get; set; }
    public string? Note     { get; set; }
    public string? Purpose  { get; set; }
    public double? Blane    { get; set; }
    public long?   BudgetId { get; set; }
}
public class FundSearchRequest : SearchRequest { }

// ==================== GEOGRAPHIC_AREAS ====================
public class GeographicAreasRequest
{
    public string? Code          { get; set; }
    public string? VnDescription { get; set; }
    public string? Description   { get; set; }
}
public class GeographicAreasSearchRequest : SearchRequest { }

// ==================== INVENTORY ====================
public class InventoryRequest
{
    public string?   InventoryName { get; set; }
    public DateTime? Submited      { get; set; }
    public int?      Status        { get; set; }
    public long?     UserId        { get; set; }
}
public class InventorySearchRequest : SearchRequest
{
    /// <summary>Lọc theo ngày kiểm kê (<c>Submited</c>), tính cả 2 đầu (port ELIB-LRC 10-04).</summary>
    public DateTime? InventoryDateFrom { get; set; }
    public DateTime? InventoryDateTo   { get; set; }
}

// ==================== INVENTORY_BARCODE ====================
public class InventoryBarcodeRequest
{
    public string? Barcode          { get; set; }
    public long?   StoreId          { get; set; }
    public long?   InventoryId      { get; set; }
    public int?    CheckStoreStatus { get; set; }
    public int?    CheckStatus      { get; set; }
    public int?    CheckBorrow      { get; set; }
    public int?    CheckRegisteter  { get; set; }
}
public class InventoryBarcodeSearchRequest : SearchRequest { }

// ==================== ISBD_FIELD ====================
public class IsbdFieldRequest
{
    public int?    Config_Id  { get; set; }
    public string? Field      { get; set; }
    public string? Starttp    { get; set; }
    public string? Stoptp     { get; set; }
    public int?    Fieldindex { get; set; }
}
public class IsbdFieldSearchRequest : SearchRequest { }

// ==================== ISBD_SUBFIELD ====================
public class IsbdSubfieldRequest
{
    public int?    Config_Id     { get; set; }
    public string? Field         { get; set; }
    public string? Subfield      { get; set; }
    public string? Starttp       { get; set; }
    public string? Stoptp        { get; set; }
    public string? Nexttp        { get; set; }
    public int?    Subfieldindex { get; set; }
}
public class IsbdSubfieldSearchRequest : SearchRequest { }

// ==================== PB_KEY ====================
public class PbKeyRequest
{
    public string? Machiakhoa  { get; set; }
    public string? Tenchiakhoa { get; set; }
}
public class PbKeySearchRequest : SearchRequest { }

// ==================== KEY_IN ====================
public class KeyInRequest
{
    public long?     Keyid      { get; set; }
    public long?     Readerid   { get; set; }
    public DateTime? BorrowDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public long?     Userid     { get; set; }
    public string?   Note       { get; set; }
}
public class KeyInSearchRequest : SearchRequest { }

// ==================== KEY_OUT ====================
public class KeyOutRequest
{
    public long?     Keyid      { get; set; }
    public long?     Readerid   { get; set; }
    public DateTime? BorrowDate { get; set; }
    public long?     Userid     { get; set; }
    public string?   Note       { get; set; }
}
public class KeyOutSearchRequest : SearchRequest { }

// ==================== KIEM_KE ====================
public class KiemKeRequest
{
    public string?   Dkcb    { get; set; }
    public int?      StoreId { get; set; }
    public DateTime? Submited { get; set; }
}
public class KiemKeSearchRequest : SearchRequest { }

// ==================== PB_LANGUAGE ====================
public class LanguageRequest
{
    public string? Code          { get; set; }
    public string? VnDescription { get; set; }
    public string? Description   { get; set; }
}
public class LanguageSearchRequest : SearchRequest { }

// ==================== LINH_VUC_NGHIEN_CUU ====================
public class LinhVucNghienCuuRequest
{
    public string? Name     { get; set; }
    public string? Ma       { get; set; }
    public int?    ParentId { get; set; }
    public string? GhiChu  { get; set; }
}
public class LinhVucNghienCuuSearchRequest : SearchRequest { }

// ==================== LOG_BIEN_MUC ====================
public class LogBienMucRequest
{
    public long?     UserId   { get; set; }
    public DateTime? Submited { get; set; }
    public string?   Status   { get; set; }
    public long?     BibId    { get; set; }
}
public class LogBienMucSearchRequest : SearchRequest { }

// ==================== LOST_BOOK ====================
public class LostBookRequest
{
    public string?   Barcode   { get; set; }
    public DateTime? Submited  { get; set; }
    public long?     Store     { get; set; }
    public long?     CreatedBy { get; set; }
    public string?   Reason    { get; set; }
}
public class LostBookSearchRequest : SearchRequest
{
    public long?     StoreId         { get; set; }
    public DateTime? CreatedDateFrom { get; set; }
    public DateTime? CreatedDateTo   { get; set; }
    public string?   Barcode         { get; set; } // Đăng ký cá biệt
    public long?     BibTypeId       { get; set; } // Loại tài liệu
    public long?     MfnFrom         { get; set; }
    public long?     MfnTo           { get; set; }
}

// ==================== LYDOPHAT ====================
public class LydophatRequest { public string? Name { get; set; } }
public class LydophatSearchRequest : SearchRequest { }

// ==================== MAGAZINE_TYPE ====================
public class MagazineTypeRequest { public string? Name { get; set; } }
public class MagazineTypeSearchRequest : SearchRequest { }

// ==================== MARC_CODE_LIST ====================
public class MarcCodeListRequest
{
    public string? Loai  { get; set; }
    public string? Ma    { get; set; }
    public string? Desvn { get; set; }
    public string? Desta { get; set; }
}
public class MarcCodeListSearchRequest : SearchRequest { }

// ==================== MARC_FIELD ====================
public class MarcFieldRequest
{
    public string? Field        { get; set; }
    public string? Description  { get; set; }
    public string? Vndescription { get; set; }
    public int?    Repeatable   { get; set; }
    public int?    MANDATORY    { get; set; }
}
public class MarcFieldSearchRequest : SearchRequest { }

// ==================== MARC_INDICATOR ====================
public class MarcIndicatorRequest
{
    public string? INDICATOR     { get; set; }
    public string? Value         { get; set; }
    public string? Description   { get; set; }
    public string? VNDESCRIPTION { get; set; }
    public string? Field_Id      { get; set; }
}
public class MarcIndicatorSearchRequest : SearchRequest { }

// ==================== MARC_SUBFIELD ====================
public class MarcSubFieldRequest
{
    public string? Field         { get; set; }
    public string? Subfield      { get; set; }
    public string? Description   { get; set; }
    public string? Vndescription { get; set; }
    public int?    Repeatable    { get; set; }
    public int?    MANDATORY     { get; set; }
}
public class MarcSubFieldSearchRequest : SearchRequest { }

// ==================== MARC_BIB_LEVEL ====================
public class MarcBibLevelRequest
{
    public string? Code          { get; set; }
    public string? VnDescription { get; set; }
    public string? Description   { get; set; }
}
public class MarcBibLevelSearchRequest : SearchRequest { }

// ==================== MARC_RECORD_TYPE ====================
public class MarcRecordTypeRequest
{
    public string? MarcTypeCode   { get; set; }
    public string? RecordTypeCode { get; set; }
}
public class MarcRecordTypeSearchRequest : SearchRequest { }

// ==================== MARC_TYPE ====================
public class MarcTypeRequest
{
    public string? Code          { get; set; }
    public string? VnDescription { get; set; }
    public string? Description   { get; set; }
}
public class MarcTypeSearchRequest : SearchRequest { }

// ==================== MATERIALS_TYPE ====================
public class MaterialsTypeRequest
{
    public string? Code          { get; set; }
    public string? VnDescription { get; set; }
    public string? Description   { get; set; }
}
public class MaterialsTypeSearchRequest : SearchRequest { }

// ==================== ORDER_STATUS ====================
public class OrderStatusRequest { public string? Name { get; set; } }
public class OrderStatusSearchRequest : SearchRequest { }

// ==================== PARTEM_MAGAZINE_DETAIL ====================
public class PartemMagazineDetailRequest
{
    public long?   PatternId { get; set; }
    public string? X        { get; set; }
    public string? Y        { get; set; }
    public string? Z        { get; set; }
    public int?    StepX    { get; set; }
    public int?    StepY    { get; set; }
    public int?    StepZ    { get; set; }
    public int?    RepeatX  { get; set; }
    public int?    RepeatY  { get; set; }
    public int?    RepeatZ  { get; set; }
    public int?    MaxX     { get; set; }
    public int?    MaxY     { get; set; }
    public int?    MaxZ     { get; set; }
    public int?    ResetX   { get; set; }
    public int?    ResetY   { get; set; }
    public int?    ResetZ   { get; set; }
}
public class PartemMagazineDetailSearchRequest : SearchRequest { }

// ==================== PATTERN_MAGAZINE ====================
public class PatternMagazineRequest
{
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public string? Function    { get; set; }
    public int?    Order       { get; set; }
}
public class PatternMagazineSearchRequest : SearchRequest { }

// ==================== PHONG_BAN ====================
public class PhongBanRequest { public string? Name { get; set; } }
public class PhongBanSearchRequest : SearchRequest { }

// ==================== POLICY_CIRC ====================
public class PolicyCircRequest
{
    public int? ReaderType      { get; set; }
    public int? CircPlace       { get; set; }
    public int? NumberOfDate    { get; set; }
    public int? NumberOfRenew   { get; set; }
    public int? NumberOfBook    { get; set; }
    public int? NumberOfRequest { get; set; }
    public int? Muontrung       { get; set; }
    public int? Store           { get; set; }
    public int? NumberOfBookAccept   { get; set; }
    public int? NumberOfRenewQty     { get; set; }
    public int? NumberOfRenewDays    { get; set; }
    public int? NumberOfRequestCount { get; set; }
    public int? NumberOfRequestDays  { get; set; }
    public int? AllowOpacRequest     { get; set; }
}
public class PolicyCircSearchRequest : SearchRequest
{
    public int? CircPlace  { get; set; }
    public int? ReaderType { get; set; }
}

// ==================== PRINTBOOK_AND_DIGITAL ====================
public class PrintBookAndDigitalRequest
{
    public long? BibId   { get; set; }
    public long? EbookId { get; set; }
}
public class PrintBookAndDigitalSearchRequest : SearchRequest
{
    public long? BibId   { get; set; }
    public long? EbookId { get; set; }
}
public class PrintBookAndDigitalLinkRequest
{
    public long? BibId   { get; set; }
    public long? EbookId { get; set; }
}

// ==================== PB_PRIVATE ====================
public class PbPrivateRequest
{
    public int? RoleId   { get; set; }
    public int? ModuleId { get; set; }
}
public class PbPrivateSearchRequest : SearchRequest { }

// ==================== RECEIPT_STATUS ====================
public class ReceiptStatusRequest { public string? Name { get; set; } }
public class ReceiptStatusSearchRequest : SearchRequest { }

// ==================== RECORD_TYPE ====================
public class RecordTypeRequest
{
    public string? Code          { get; set; }
    public string? VnDescription { get; set; }
    public string? Description   { get; set; }
}
public class RecordTypeSearchRequest : SearchRequest { }

// ==================== PB_ROLES ====================
public class PbRolesRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
}
public class PbRolesSearchRequest : SearchRequest { }

// ==================== SERIAL ====================
public class SerialRequest
{
    public long?     BibId       { get; set; }
    public DateTime? StartTime   { get; set; }
    public DateTime? EndTime     { get; set; }
    public long?     FrequencyId { get; set; }
    public long?     PatternId   { get; set; }
    public long?     StoreId     { get; set; }
    public string?   Note        { get; set; }
    public int?      LastX       { get; set; }
    public int?      LastY       { get; set; }
    public int?      LastZ       { get; set; }
    public int?      IssueLength { get; set; }
    public int?      WeekLength  { get; set; }
    public long?     SupplierId  { get; set; }
    public int?      MonthLenght { get; set; }
    public string?   Locate      { get; set; }
    public DateTime? FirstTime   { get; set; }
    public int?      StartX      { get; set; }
    public int?      StartY      { get; set; }
    public int?      StartZ      { get; set; }
}
public class SerialSearchRequest : SearchRequest { }

// ==================== SERIAL_ITEM ====================
public class SerialItemRequest
{
    public long?     SUBSCRIPTION_ID { get; set; }
    public string?   SERIAL_SEQ      { get; set; }
    public int?      SERIAL_SEQ_X    { get; set; }
    public int?      SERIAL_SEQ_Y    { get; set; }
    public int?      SERIAL_SEQ_Z    { get; set; }
    public int?      IS_SPECIAL      { get; set; }
    public int?      STATUS          { get; set; }
    public int?      QUANTITY        { get; set; }
    public DateTime? PLANNED_DATE    { get; set; }
    public string?   NOTE            { get; set; }
    public DateTime? PUBLISHED_DATE  { get; set; }
    public DateTime? CLAIM_DATE      { get; set; }
    public int?      CLAIM_COUNT     { get; set; }
    public bool?     IsMerged        { get; set; }
    public int?      SortOrder       { get; set; }
}
public class SerialItemSearchRequest : SearchRequest { }

public class SerialApproveRequest { public Guid PublicId { get; set; } }

// ==================== STORE ====================
public class StoreRequest
{
    public string? Name        { get; set; }
    public string? Postion     { get; set; }
    public long?   StoreTypeId { get; set; }
    public string? Code        { get; set; }
    public string? Images      { get; set; }
}
public class StoreSearchRequest : SearchRequest { }

// ==================== STORE_TYPE ====================
public class StoreTypeRequest
{
    public string? Name     { get; set; }
    public long?   ParentId { get; set; }
}
public class StoreTypeSearchRequest : SearchRequest { }

// ==================== SUBCRIPTION_STATUS ====================
public class SubcriptionStatusRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class SubcriptionStatusSearchRequest : SearchRequest { }

// ==================== SUPPLIER ====================
public class SupplierRequest
{
    public string? Name           { get; set; }
    public string? Address        { get; set; }
    public string? Mobile         { get; set; }
    public string? Fax            { get; set; }
    public string? Account        { get; set; }
    public string? Bank           { get; set; }
    public string? Mst            { get; set; }
    public string? Email          { get; set; }
    public string? Website        { get; set; }
    public string? Position       { get; set; }
    public string? Representative { get; set; }
}
public class SupplierSearchRequest : SearchRequest { }

// ==================== SYSTEM_DESCRIPTION ====================
public class SystemDescriptionRequest
{
    public string? Name  { get; set; }
    public string? Descvn { get; set; }
    public string? Val   { get; set; }
    public string? Unit  { get; set; }
}
public class SystemDescriptionSearchRequest : SearchRequest { }

// ==================== SYSTEM_INFO ====================
public class SystemInfoRequest
{
    public string? LibraryName { get; set; }
    public string? Address     { get; set; }
    public string? Tel         { get; set; }
}
public class SystemInfoSearchRequest : SearchRequest { }

// ==================== SYSTEM_PARA ====================
public class SystemParaRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
}
public class SystemParaSearchRequest : SearchRequest { }

// ==================== THANHLY ====================
public class ThanhlyRequest
{
    public long?     BarcodeId { get; set; }
    public DateTime? Sumited   { get; set; }
    public int?      UserId    { get; set; }
    public string?   Note      { get; set; }
}
public class ThanhlySearchRequest : SearchRequest
{
    public string?   Title         { get; set; }
    public string?   Author        { get; set; }
    public string?   Barcode       { get; set; }
    public long?     StoreId       { get; set; }
    public long?     BibTypeId     { get; set; }
    public DateTime? LiquidateFrom { get; set; }
    public DateTime? LiquidateTo   { get; set; }
}

// ==================== TRACKING_TO_LIBRARY ====================
public class TrackingToLibraryRequest
{
    public long?     Readerid { get; set; }
    public long?     UserId   { get; set; }
    public DateTime? Time     { get; set; }
    public long?     StoreId  { get; set; }
}
public class TrackingToLibrarySearchRequest : SearchRequest { }

// ==================== WORKSHEET_FIELD ====================
public class WorksheetFieldRequest
{
    public int?    Bib_Worksheet_Id { get; set; }
    public string? Field            { get; set; }
    public string? L1               { get; set; }
    public string? L2               { get; set; }
}
public class WorksheetFieldSearchRequest : SearchRequest { }

// ==================== WORKSHEET_SUBFIELD ====================
public class WorksheetSubfieldRequest
{
    public long?   Worksheet_Field_Id { get; set; }
    public string? Subfield           { get; set; }
    public string? Value              { get; set; }
}
public class WorksheetSubfieldSearchRequest : SearchRequest { }

// ==================== Z3950_GROUP ====================
public class Z3950GroupRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class Z3950GroupSearchRequest : SearchRequest { }

// ==================== Z3950_CONFIG ====================
public class Z3950ConfigRequest
{
    public string? Name         { get; set; }
    public string? Host         { get; set; }
    public string? Port         { get; set; }
    public string? DatabaseName { get; set; }
    public string? Systax       { get; set; }
    public string? UserName     { get; set; }
    public string? Password     { get; set; }
    public string? PortalId     { get; set; }
    public string? Language     { get; set; }
    public long?   GroupId      { get; set; }
    public string? Url          { get; set; }
}
public class Z3950ConfigSearchRequest : SearchRequest { }

// ==================== D_EXPORT_REASON / D_BOOK_OUT_UNIT / D_EXHIBITION_LOCATION ====================
public class DExportReasonRequest { public string? Name { get; set; } }
public class DExportReasonSearchRequest : SearchRequest { }

public class DBookOutUnitRequest { public string? Name { get; set; } }
public class DBookOutUnitSearchRequest : SearchRequest { }

public class DExhibitionLocationRequest { public string? Name { get; set; } }
public class DExhibitionLocationSearchRequest : SearchRequest { }

// ==================== BOOK_OUT_STORE ====================
public class BookOutStoreRequest
{
    public long?     BarcodeId              { get; set; }
    public string?   DelivererName          { get; set; }
    public string?   ReceiverName           { get; set; }
    public int?      ReasonId               { get; set; }
    public int?      UnitId                 { get; set; }
    public int?      ExhibitionLocationId   { get; set; }
    public bool      ReturnBarcodeAtLibrary { get; set; }
    public int?      Store                  { get; set; }
    public DateTime? ExportDate             { get; set; }
    public DateTime? ImportDate             { get; set; }
    public string?   Status                 { get; set; }
}
public class BookOutStoreSearchRequest : SearchRequest
{
    public string?   TypeStatus              { get; set; } // "O" Sách ra kho / "R" Sách vào kho
    public int?      UnitId                  { get; set; }
    public int?      ReasonId                { get; set; }
    public int?      ExhibitionLocationId    { get; set; }
    public string?   DelivererName           { get; set; }
    public string?   ReceiverName            { get; set; }
    public string?   Barcode                 { get; set; }
    public bool?     ReturnBarcodeAtLibrary  { get; set; }
    public DateTime? ExportDateFrom          { get; set; }
    public DateTime? ExportDateTo            { get; set; }
    public DateTime? ImportDateFrom          { get; set; }
    public DateTime? ImportDateTo            { get; set; }
}

public class ScanOutRequest
{
    public string?  Barcode                { get; set; }
    public string?  DelivererName          { get; set; }
    public string?  ReceiverName           { get; set; }
    public int?     ReasonId               { get; set; }
    public int?     UnitId                 { get; set; }
    public int?     ExhibitionLocationId   { get; set; }
    public bool     ReturnBarcodeAtLibrary { get; set; }
}
public class ScanInRequest { public string? Barcode { get; set; } }
