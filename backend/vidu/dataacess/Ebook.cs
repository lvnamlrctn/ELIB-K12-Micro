using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
namespace DataAccess.Ebook
{
    public class Ebook
    {
        Framework.Database.SqlDatabaseHelper objSqlHelper = null;

        public Ebook()
        {
            objSqlHelper = new Framework.Database.SqlDatabaseHelper();
        }
        public Ebook(string ConnectionString)
        {
            objSqlHelper = new Framework.Database.SqlDatabaseHelper(ConnectionString);
        }
        public DataTable ListTaiLieuNoiBat(int top)
        {

            DataTable dtDEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("top",top,"in") };


            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtDEbook = objSqlHelper.ExecuteDataTable("Ebook.ListTailieunoibat", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtDEbook;
        }
        public DataTable BrowseBy(string By, string Value)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("by",By,"in"),
                new Framework.Database.DatabaseParamCls("value",Value,"in")
                

             };
            DataTable dtEbook = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        if (By != "")
                        {

                            dtEbook = objSqlHelper.ExecuteDataTable("Ebook.BrowseBy", CommandType.StoredProcedure, arrParams);

                        }
                        break;
                    }
            }
            return dtEbook;
        }
        public void SaveBookLog(string CardNumber, long ReaderId, long BookId, long Page, long Size, int Type, string Ip)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
            
                new Framework.Database.DatabaseParamCls("ReaderId",ReaderId,"in"),
                new Framework.Database.DatabaseParamCls("CardNumber",CardNumber,"in"),
                new Framework.Database.DatabaseParamCls("BookId",BookId,"in"),
                new Framework.Database.DatabaseParamCls("Page",Page,"in"),
                new Framework.Database.DatabaseParamCls("Size",Size,"in"),
                new Framework.Database.DatabaseParamCls("Type",Type,"in"),
                new Framework.Database.DatabaseParamCls("Ip",Ip,"in"),
                
                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.SaveEBookLog", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }

        }
        public void SaveBookLogLRC(string CardNumber, long ReaderId, long BookId, long Page, long Size, int Type, string Ip)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
            
                new Framework.Database.DatabaseParamCls("ReaderId",ReaderId,"in"),
                new Framework.Database.DatabaseParamCls("CardNumber",CardNumber,"in"),
                new Framework.Database.DatabaseParamCls("BookId",BookId,"in"),
                new Framework.Database.DatabaseParamCls("Page",Page,"in"),
                new Framework.Database.DatabaseParamCls("Size",Size,"in"),
                new Framework.Database.DatabaseParamCls("Type",Type,"in"),
                new Framework.Database.DatabaseParamCls("Ip",Ip,"in"),
                
                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.SaveEBookLogLRC", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }

        }
        public string AddBookItem(long Id, int AllowDownload, int Status, long CollectionId, long SubjectId, long TypeId, long CreatedBy, long UpdateBy, string Images)
        {
            String[] Output = new String[10];

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",Id,"in"),
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId,"in"),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId,"in"),
                new Framework.Database.DatabaseParamCls("createdBy",CreatedBy,"in"),
                new Framework.Database.DatabaseParamCls("status",Status,"in"),
                new Framework.Database.DatabaseParamCls("TypeId",TypeId,"in"),
                new Framework.Database.DatabaseParamCls("UpdateBy",UpdateBy,"in"),
                new Framework.Database.DatabaseParamCls("AllowDownload",AllowDownload,"in"),
                new Framework.Database.DatabaseParamCls("images",Images,"in"),
                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.EditEbook", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
            return Output[0].ToString();


        }
        /// <summary>
        /// Thay đổi bộ sưu tập và Chủ để
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="CollectionId"></param>
        /// <param name="SubjectId"></param>
        public void UpdateCollection(long Id, long CollectionId, long SubjectId, long ToPicId)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",Id,"in"),
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId,"in"),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId,"in"),
                new Framework.Database.DatabaseParamCls("ToPicId",ToPicId,"in"),
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        objSqlHelper.ExecuteQuery(null, "Ebook.UpdateCollectionAdnSubject", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
        }
        public DataTable GetTitle(long EbookId)
        {
            DataTable dtEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("EbookId",EbookId),

         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbook = objSqlHelper.ExecuteDataTable("select * from Ebook.ItemXml Where Id="+EbookId.ToString(), CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtEbook;
        }
        public DataTable GetBriefFileEbook(long EbookId)
        {
            DataTable dtEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("EbookId",EbookId),
                
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbook = objSqlHelper.ExecuteDataTable("select * from EbookFile", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtEbook;
        }
        public void UpdateTotalDownload(long Id)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",Id,"in"),
                
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        objSqlHelper.ExecuteQuery(null, "Update Ebook.item set totalDownload=totalDownload+1 where id=@id", arrParams, CommandType.Text);
                        break;
                    }
            }

        }

        public void UpdateTotalView(long Id)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",Id,"in"),
                
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        objSqlHelper.ExecuteQuery(null, "Update Ebook.item set totalView=TotalView+1 where id="+Id.ToString(), arrParams, CommandType.Text);
                        break;
                    }
            }

        }
        public DataTable GetDigType(long digTypeId)
        {
            DataTable dtEbook = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbook = objSqlHelper.ExecuteDataTable("select * from Ebook.DigType Where Id="+ digTypeId.ToString());
                        break;
                    }
            }
            return dtEbook;
        }
        /// <summary>
        /// Lấy danh sách sách mới
        /// </summary>
        /// <param name="NumberEbook">Số lượng sách muốn láy</param>
        /// <returns></returns>
        public DataTable ListNewEBook(string NumberEbook)
        {
            DataTable dtDEbook = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("Top",NumberEbook,"in"),
                
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtDEbook = objSqlHelper.ExecuteDataTable("Ebook.ListNewEBook", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtDEbook;
        }
        public DataTable ListNamTapChi(string NumberEbook,string Collection="")
        {
            DataTable dtDEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("CollectionId",Collection,"in"),

             };

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtDEbook = objSqlHelper.ExecuteDataTable("Ebook.ListNamTapChi", CommandType.StoredProcedure,arrParams);
                        break;
                    }
            }
            return dtDEbook;
        }
        public DataTable ListNewPBook(string NumberEbook)
        {
            DataTable dtDEbook = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
            new Framework.Database.DatabaseParamCls("Top",NumberEbook,"in"),

            };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtDEbook = objSqlHelper.ExecuteDataTable("PrintBook.ListNewPBook", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtDEbook;
        }

        public DataTable ListTaiLieuLienQuan(string NumberEbook, int collection)
        {
            DataTable dtDEbook = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("Top",NumberEbook,"in"),
                new Framework.Database.DatabaseParamCls("collectionID",collection,"in"),
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtDEbook = objSqlHelper.ExecuteDataTable("Ebook.ListTaiLieuLienQuan", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtDEbook;
        }

        public DataTable GetDataTableEbookById(long Id)
        {
            DataTable temp = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        temp = objSqlHelper.ExecuteDataTable("Ebook.GetEbookByID", CommandType.StoredProcedure,
                             new Framework.Database.DatabaseParamCls[] { new Framework.Database.DatabaseParamCls("id", Id) });
                        break;
                    }
            }
            return temp;
        }
        public void ChangeEbookShow(long Id, int Status)
        {
            Status = 3 - Status;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",Id,"in"),
                new Framework.Database.DatabaseParamCls("status",Status,"in"),
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        objSqlHelper.ExecuteQuery(null, "Ebook.UpdateEbookShow", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
        }
        public void ChangeEbookStatus(long Id, int Status)
        {
            Status = 3 - Status;
            if ((Status != 1) && (Status != 2))
            {
                Status = 1;
            }
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",Id,"in"),
                new Framework.Database.DatabaseParamCls("status",Status,"in"),
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        objSqlHelper.ExecuteQuery(null, "Ebook.UpdateEbookStatus", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
        }
        /// <summary>
        /// Tìm kiếm tài liệu thông thường
        /// </summary>
        /// <param name="Title"></param>
        /// <param name="Author"></param>
        /// <param name="Publisher"></param>
        /// <param name="PublishDate"></param>
        /// <param name="Keyword"></param>
        /// <param name="SubmitedFrom"></param>
        /// <param name="SubmitedTo"></param>
        /// <param name="UserId"></param>
        /// <param name="CollectionId"></param>
        /// <param name="status"></param>
        /// <param name="TopicId"></param>
        /// <param name="Id"></param>
        /// <param name="Top"></param>
        /// <param name="OrderBy"></param>
        /// <param name="PortalId"></param>
        /// <param name="Language"></param>
        /// <returns></returns>
        public DataTable SearchEbook(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, string publishDateFrom = "", string publishDateTo = "",long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", long ItemPerPage = 0, long CurrentPage = 0, string TenTapChi = "", string SoTapChi = "")
        {

            DataTable dt = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
               new  Framework.Database.DatabaseParamCls("publishDateFrom", publishDateFrom),
                new  Framework.Database.DatabaseParamCls("publishDateTo", publishDateTo),
                new Framework.Database.DatabaseParamCls("ItemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage)
         };
            //if (!string.IsNullOrEmpty(TenTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi);
            //}
            //if (!string.IsNullOrEmpty(SoTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi);
            //}

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        if (ItemPerPage == 0)
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook", CommandType.StoredProcedure, arrParams);
                        }
                        else
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook1", CommandType.StoredProcedure, arrParams);
                        }

                        break;
                    }
            }
            return dt;
        }

        public DataTable SearchEbookValuate(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId=0, long monHocId=0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", long ItemPerPage = 0, long CurrentPage = 0, string TenTapChi = "", string SoTapChi = "",long DigTypeId=0)
        {

            DataTable dt = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
               new  Framework.Database.DatabaseParamCls("publishDateFrom", publishDateFrom),
                new  Framework.Database.DatabaseParamCls("publishDateTo", publishDateTo),
                new Framework.Database.DatabaseParamCls("ItemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
                 new Framework.Database.DatabaseParamCls("NganhHocId",nganhHocId),
                new Framework.Database.DatabaseParamCls("monHocId",monHocId),
                new Framework.Database.DatabaseParamCls("programId",0),
                new Framework.Database.DatabaseParamCls("DigTypeId",DigTypeId),
                 new Framework.Database.DatabaseParamCls("itemIdList",""),
         };
            
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        if (ItemPerPage == 0)
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook", CommandType.StoredProcedure, arrParams);
                        }
                        else
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook1", CommandType.StoredProcedure, arrParams);
                        }

                        break;
                    }
            }
            return dt;
        }
        public DataTable SearchEbookT36(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId = 0, long monHocId = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", long ItemPerPage = 0, long CurrentPage = 0, string TenTapChi = "", string SoTapChi = "", long DigTypeId = 0,string DoMat="")
        {

            DataTable dt = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
               new  Framework.Database.DatabaseParamCls("publishDateFrom", publishDateFrom),
                new  Framework.Database.DatabaseParamCls("publishDateTo", publishDateTo),
                new Framework.Database.DatabaseParamCls("ItemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
                 new Framework.Database.DatabaseParamCls("NganhHocId",nganhHocId),
                new Framework.Database.DatabaseParamCls("monHocId",monHocId),
                new Framework.Database.DatabaseParamCls("programId",0),
                new Framework.Database.DatabaseParamCls("DigTypeId",DigTypeId),
                 new Framework.Database.DatabaseParamCls("itemIdList",""),
                 new Framework.Database.DatabaseParamCls("DoMat",DoMat),
         };

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        if (ItemPerPage == 0)
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook", CommandType.StoredProcedure, arrParams);
                        }
                        else
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook1", CommandType.StoredProcedure, arrParams);
                        }

                        break;
                    }
            }
            return dt;
        }
        public DataTable SearchEbookHeritage(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId = 0, long monHocId = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", long ItemPerPage = 0, long CurrentPage = 0, string TenTapChi = "", string SoTapChi = "", long DigTypeId = 0,string ItemList="")
        {

            DataTable dt = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
               new  Framework.Database.DatabaseParamCls("publishDateFrom", publishDateFrom),
                new  Framework.Database.DatabaseParamCls("publishDateTo", publishDateTo),
                new Framework.Database.DatabaseParamCls("ItemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
                 new Framework.Database.DatabaseParamCls("NganhHocId",nganhHocId),
                new Framework.Database.DatabaseParamCls("monHocId",monHocId),
                new Framework.Database.DatabaseParamCls("programId",0),
                new Framework.Database.DatabaseParamCls("DigTypeId",DigTypeId),
                new Framework.Database.DatabaseParamCls("itemIdList",ItemList),
         };

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        if (ItemPerPage == 0)
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook", CommandType.StoredProcedure, arrParams);
                        }
                        else
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook1", CommandType.StoredProcedure, arrParams);
                        }

                        break;
                    }
            }
            return dt;
        }
        public DataTable ThongKeTacGia(string Where)
        {

            DataTable dt = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("Where",Where),
               
         };

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                       
                            dt = objSqlHelper.ExecuteDataTable("ebook.ThongKeTacGia", CommandType.StoredProcedure, arrParams);
                        

                        break;
                    }
            }
            return dt;
        }
        public DataTable SearchEbookOAI( string SubmitedFrom, string SubmitedTo,  long CollectionId, int status,  long ItemPerPage = 0, long CurrentPage = 0)
        {

            DataTable dt = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
               
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
               
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                
                new Framework.Database.DatabaseParamCls("ItemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
               
         };

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        if (ItemPerPage == 0)
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbookOAI", CommandType.StoredProcedure, arrParams);
                        }
                        else
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbookOAIPhanTrang", CommandType.StoredProcedure, arrParams);
                        }

                        break;
                    }
            }
            return dt;
        }
        public DataTable SearchEbooklrc(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", int ItemPerPage = 0, int CurrentPage = 0, string TenTapChi = "", string SoTapChi = "",long ProjectId=0)
        {

            DataTable dt = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
               new  Framework.Database.DatabaseParamCls("ProjectId", ProjectId),
                new Framework.Database.DatabaseParamCls("ItemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
         };
           

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        if (ItemPerPage == 0)
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook", CommandType.StoredProcedure, arrParams);
                        }
                        else
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook1", CommandType.StoredProcedure, arrParams);
                        }

                        break;
                    }
            }
            return dt;
        }
        public DataTable SearchEbookBaoND(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", int ItemPerPage = 0, int CurrentPage = 0, string TenTapChi = "", string SoTapChi = "", long MagazineId = 0, long SoTapChiId = 0, string NgayPhatHanhFrom = "", string NgayPhatHanhTo = "")
        {

            DataTable dt = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChiId", SoTapChiId),
              new  Framework.Database.DatabaseParamCls("NgayPhatHanhFrom", NgayPhatHanhFrom),
              new  Framework.Database.DatabaseParamCls("NgayPhatHanhTo", NgayPhatHanhTo),
                new Framework.Database.DatabaseParamCls("ItemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
         };
            //if (!string.IsNullOrEmpty(TenTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi);
            //}
            //if (!string.IsNullOrEmpty(SoTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi);
            //}

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        if (ItemPerPage == 0)
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook", CommandType.StoredProcedure, arrParams);
                        }
                        else
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook1", CommandType.StoredProcedure, arrParams);
                        }

                        break;
                    }
            }

            return dt;
        }
        public DataTable SearchEbookTapChiChanNuoi(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", int ItemPerPage = 0, int CurrentPage = 0, string TenTapChi = "", string SoTapChi = "", long MagazineId = 0, long SoTapChiId = 0)
        {

            DataTable dt = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChiId", SoTapChiId),
                new Framework.Database.DatabaseParamCls("ItemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
         };
            //if (!string.IsNullOrEmpty(TenTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi);
            //}
            //if (!string.IsNullOrEmpty(SoTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi);
            //}

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        if (ItemPerPage == 0)
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook", CommandType.StoredProcedure, arrParams);
                        }
                        else
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook1", CommandType.StoredProcedure, arrParams);
                        }

                        break;
                    }
            }
            
            return dt;
        }
        /// <summary>
        /// Tìm kiếm tài liệu CSDL Bài báo tạp chí
        /// </summary>
        /// <param name="Title"></param>
        /// <param name="Author"></param>
        /// <param name="Publisher"></param>
        /// <param name="PublishDate"></param>
        /// <param name="Keyword"></param>
        /// <param name="SubmitedFrom"></param>
        /// <param name="SubmitedTo"></param>
        /// <param name="UserId"></param>
        /// <param name="CollectionId"></param>
        /// <param name="status"></param>
        /// <param name="TopicId"></param>
        /// <param name="Id"></param>
        /// <param name="Top"></param>
        /// <param name="OrderBy"></param>
        /// <param name="PortalId"></param>
        /// <param name="Language"></param>
        /// <returns></returns>
        public DataTable SearchArticle(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long Id = 0, string AuthorId = "", string OrgId = "", string Top = "", string OrderBy = "", string PortalId = "", string Language = "")
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                
                new Framework.Database.DatabaseParamCls("AuthorId",AuthorId),
                new Framework.Database.DatabaseParamCls("OrgId",OrgId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbook", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            return dt;
        }
        /// <summary>
        /// Tìm kiếm trong cơ sở dữ liệu bài báo theo ajax
        /// </summary>
        /// <param name="Title"></param>
        /// <param name="Author"></param>
        /// <param name="Publisher"></param>
        /// <param name="PublishDate"></param>
        /// <param name="Keyword"></param>
        /// <param name="SubmitedFrom"></param>
        /// <param name="SubmitedTo"></param>
        /// <param name="UserId"></param>
        /// <param name="CollectionId"></param>
        /// <param name="status"></param>
        /// <param name="TopicId"></param>
        /// <param name="Id"></param>
        /// <param name="AuthorId"></param>
        /// <param name="OrgId"></param>
        /// <param name="Top"></param>
        /// <param name="OrderBy"></param>
        /// <param name="PortalId"></param>
        /// <param name="Language"></param>
        /// <returns></returns>
        public DataTable SearchArticle(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, int ItemPerPage, int CurrentPage, long Id = 0, string AuthorId = "", string OrgId = "", string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string ArticleName = "")
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                
                new Framework.Database.DatabaseParamCls("AuthorId",AuthorId),
                new Framework.Database.DatabaseParamCls("OrgId",OrgId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                 new Framework.Database.DatabaseParamCls("itemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
                new Framework.Database.DatabaseParamCls("ArticleName",ArticleName),
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbookPhanTrang", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            return dt;
        }


        public DataTable SearchMagazineDetail(long MagazineId, int ItemPerPage, int CurrentPage)
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("MagazineId",MagazineId),
               

             
                 new Framework.Database.DatabaseParamCls("itemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
             
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.SearchMagazineDetail", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            return dt;
        }

        public DataTable SearchMagazineByYear(string Year, long CollectionId, int ItemPerPage, int CurrentPage)
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("Year",Year),
                new Framework.Database.DatabaseParamCls("CollectionId",CollectionId),


                

         };
            if (ItemPerPage == 0)
            {


                
            }
            else
            {
                Array.Resize(ref arrParams, arrParams.Length + 1);
                arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("ItemPerPage", ItemPerPage);

                Array.Resize(ref arrParams, arrParams.Length + 1);
                arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("CurrentPage", CurrentPage);

                

            }

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        if (ItemPerPage == 0)
                        {
                            dt = objSqlHelper.ExecuteDataTable("ebook.SearchMagazineByYear", CommandType.StoredProcedure, arrParams);
                        }
                        else
                        {
                           
                                dt = objSqlHelper.ExecuteDataTable("ebook.SearchMagazineByYearPhanTrang", CommandType.StoredProcedure, arrParams);
                            
                        }    

                        break;
                    }
            }
            return dt;
        }
        public long GetToTalRecordSearchMagazineByYear(string Year, long CollectionId)
        {
            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("Year",Year),
                new Framework.Database.DatabaseParamCls("CollectionId",CollectionId),

         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetToTalRecordSearchMagazineByYear", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            return Convert.ToInt64(dt.Rows[0][0].ToString());
        }
        public DataTable SearchArticleStruc()
        {

            DataTable dt = null;

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.SearchEbookStruc", CommandType.StoredProcedure, null);

                        break;
                    }
            }
            return dt;
        }

        public long GetToTalRecordArticle(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long Id = 0, string AuthorId = "", string OrgId = "", string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string ArticleName = "")
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("AuthorId",AuthorId),
                new Framework.Database.DatabaseParamCls("OrgId",OrgId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                new Framework.Database.DatabaseParamCls("ArticleName",ArticleName),
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordEbook", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            return Convert.ToInt64(dt.Rows[0][0].ToString());
        }
        public long GetToTalRecordSearchSearchMagazineDetail(long MagazineId)
        {
            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("MagazineId",MagazineId)

         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetToTalRecordSearchSearchMagazineDetail", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            return Convert.ToInt64(dt.Rows[0][0].ToString());
        }
        public long GetToTalRecordSearchEBookBasic(string strSQL)
        {
            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("strSQL",strSQL)
                
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordSearchEbookBasic", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            return Convert.ToInt64(dt.Rows[0][0].ToString());
        }
        public DataTable SearchBookBasic(string strSQL)
        {
            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("strSQL",strSQL)
                
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordSearchEbookBasic", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            return dt;
        }
        public long GetToTalRecordHVCT1(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "")
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordEbook", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            return Convert.ToInt64(dt.Rows[0][0].ToString());
        }
        public long GetToTalRecordValuate(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId = 0, long monHocId = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "",long DigTypeId=0)
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                  new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),

              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
              new  Framework.Database.DatabaseParamCls("publishDateFrom", publishDateFrom),
                new  Framework.Database.DatabaseParamCls("publishDateTo", publishDateTo),
                new Framework.Database.DatabaseParamCls("NganhHocId",nganhHocId),
                new Framework.Database.DatabaseParamCls("monHocId",monHocId),
                new Framework.Database.DatabaseParamCls("programId",0),
                new Framework.Database.DatabaseParamCls("DigTypeId",DigTypeId),
                new Framework.Database.DatabaseParamCls("itemIdList",""),
         };
          

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordEbook", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            try
            {
                return Convert.ToInt64(dt.Rows[0][0].ToString());
            }
            catch
            {
                return 0;
            }
        }
        public long GetToTalRecordT36(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId = 0, long monHocId = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "", long DigTypeId = 0,string DoMat="")
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                  new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),

              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
              new  Framework.Database.DatabaseParamCls("publishDateFrom", publishDateFrom),
                new  Framework.Database.DatabaseParamCls("publishDateTo", publishDateTo),
                new Framework.Database.DatabaseParamCls("NganhHocId",nganhHocId),
                new Framework.Database.DatabaseParamCls("monHocId",monHocId),
                new Framework.Database.DatabaseParamCls("programId",0),
                new Framework.Database.DatabaseParamCls("DigTypeId",DigTypeId),
                new Framework.Database.DatabaseParamCls("itemIdList",""),
                new Framework.Database.DatabaseParamCls("DoMat",DoMat),
         };


            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordEbook", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            try
            {
                return Convert.ToInt64(dt.Rows[0][0].ToString());
            }
            catch
            {
                return 0;
            }
        }
        public long GetToTalRecordHeritage(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId = 0, long monHocId = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "", long DigTypeId = 0,string ItemList="")
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                  new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),

              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
              new  Framework.Database.DatabaseParamCls("publishDateFrom", publishDateFrom),
                new  Framework.Database.DatabaseParamCls("publishDateTo", publishDateTo),
                new Framework.Database.DatabaseParamCls("NganhHocId",nganhHocId),
                new Framework.Database.DatabaseParamCls("monHocId",monHocId),
                new Framework.Database.DatabaseParamCls("programId",0),
                new Framework.Database.DatabaseParamCls("DigTypeId",DigTypeId),
                new Framework.Database.DatabaseParamCls("itemIdList",ItemList),
         };


            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordEbook", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            try
            {
                return Convert.ToInt64(dt.Rows[0][0].ToString());
            }
            catch
            {
                return 0;
            }
        }

        public long GetToTalRecordEBookOAI( string SubmitedFrom, string SubmitedTo,  long CollectionId, int status)
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
               
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo)
               
         };


            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordEbookOAI", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            try
            {
                return Convert.ToInt64(dt.Rows[0][0].ToString());
            }
            catch
            {
                return 0;
            }
        }

        public long GetToTalRecord(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, string publishDateFrom="", string publishDateTo="", long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "",string TenTapChi="",string SoTapChi="")
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                  new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),
                  
              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
              new  Framework.Database.DatabaseParamCls("publishDateFrom", publishDateFrom),
                new  Framework.Database.DatabaseParamCls("publishDateTo", publishDateTo),
         };
            //if (!string.IsNullOrEmpty(TenTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi);
            //}
            //if (!string.IsNullOrEmpty(SoTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi);
            //}

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordEbook", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            try
            {
                return Convert.ToInt64(dt.Rows[0][0].ToString());
            }
            catch
            {
                return 0;
            }
        }
        public long GetToTalRecordlrc(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "",long ProjectId=0)
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                new Framework.Database.DatabaseParamCls("ProjectId", ProjectId),
                  new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),

              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
             

         };
            //if (PortalId.ToUpper()=="LRCEBOOK")
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("ProjectId", 0);
            //}
            

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordEbook", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            if (Framework.Database.Table.CheckData(dt))
            {
                return Convert.ToInt64(dt.Rows[0][0].ToString());
            }
            else
            {
                return Convert.ToInt64(0);
            }
           
        }
        public long GetToTalRecordTapChiChanNuoi(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "", long MagazineId = 0, long SoTapChiId = 0)
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                  new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChiId", SoTapChiId),
         };
            //if (!string.IsNullOrEmpty(TenTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi);
            //}
            //if (!string.IsNullOrEmpty(SoTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi);
            //}

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordEbook", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            return Convert.ToInt64(dt.Rows[0][0].ToString());
        }
        public long GetToTalRecordBaoND(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long TopicId, long SubjectId = 0, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "", long MagazineId = 0, long SoTapChiId = 0,string NgayPhatHanhFrom="",string NgayPhatHanhTo="")
        {

            DataTable dt = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                new Framework.Database.DatabaseParamCls("ToPicId",TopicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("portalId",PortalId),
                new Framework.Database.DatabaseParamCls("Language",Language),
                  new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi),
              new  Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi),
               new  Framework.Database.DatabaseParamCls("NgayPhatHanhFrom", NgayPhatHanhFrom),
                new  Framework.Database.DatabaseParamCls("NgayPhatHanhTo", NgayPhatHanhTo),
              new  Framework.Database.DatabaseParamCls("SoTapChiId", SoTapChiId),
         };
            //if (!string.IsNullOrEmpty(TenTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi);
            //}
            //if (!string.IsNullOrEmpty(SoTapChi))
            //{
            //    Array.Resize(ref arrParams, arrParams.Length + 1);
            //    arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi);
            //}

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dt = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordEbook", CommandType.StoredProcedure, arrParams);

                        break;
                    }
            }
            if (Framework.Database.Table.CheckData(dt))
            {
                return Convert.ToInt64(dt.Rows[0][0].ToString());
            }
            else
            {
                return 0;
            }
        }
        /// <summary>
        /// Tìm kiếm tài liệu bằng ajax
        /// </summary>
        /// <param name="Title"></param>
        /// <param name="Author"></param>
        /// <param name="Publisher"></param>
        /// <param name="PublishDate"></param>
        /// <param name="Keyword"></param>
        /// <param name="SubmitedFrom"></param>
        /// <param name="SubmitedTo"></param>
        /// <param name="UserId"></param>
        /// <param name="CollectionId"></param>
        /// <param name="Status"></param>
        /// <param name="ToPicId"></param>
        /// <param name="ItemPerPage"></param>
        /// <param name="CurrentPage"></param>
        /// <param name="Id"></param>
        /// <param name="Top"></param>
        /// <param name="OrderBy"></param>
        /// <param name="PortalId"></param>
        /// <param name="Language"></param>
        /// <returns></returns>
        public DataTable SearchEbook(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int Status, long ToPicId, long SubjectId, int ItemPerPage, int CurrentPage, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "",string TenTapChi="", string SoTapChi="")
        {

            DataTable dtEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",Status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                 new Framework.Database.DatabaseParamCls("ToPicId",ToPicId),
                 new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                 
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("itemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
         };
            
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        dtEbook = objSqlHelper.ExecuteDataTable("ebook.SearchEbookPhanTrang", CommandType.StoredProcedure, arrParams);
                        
                        
                        break;
                    }
            }
            return dtEbook;
        }
        public DataTable SearchEbookHVCT1(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int Status, long ToPicId, long SubjectId, int ItemPerPage, int CurrentPage, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "",string ISBN="", string Extract="")
        {

            DataTable dtEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",Status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                 new Framework.Database.DatabaseParamCls("ToPicId",ToPicId),
                 new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                 
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("itemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbook = objSqlHelper.ExecuteDataTable("ebook.SearchEbookPhanTrang", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtEbook;
        }
        public DataTable SearchEbookDHY(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int Status, long ToPicId, long SubjectId, int ItemPerPage, int CurrentPage, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string Op1 = "", string Op2 = "", string Op3 = "", string Op4 = "", string Op5 = "")
        {

            DataTable dtEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),
                new Framework.Database.DatabaseParamCls("status",Status),
                new Framework.Database.DatabaseParamCls("Id",Id),
                 new Framework.Database.DatabaseParamCls("ToPicId",ToPicId),
                 new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("Top",Top),
                new Framework.Database.DatabaseParamCls("Order",OrderBy),
                new Framework.Database.DatabaseParamCls("itemPerPage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("CurrentPage",CurrentPage),
                 new Framework.Database.DatabaseParamCls("Op1",Op1),
                new Framework.Database.DatabaseParamCls("Op2",Op2),
                new Framework.Database.DatabaseParamCls("Op3",Op3),
                new Framework.Database.DatabaseParamCls("Op4",Op4),
                new Framework.Database.DatabaseParamCls("Op5",Op5),
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbook = objSqlHelper.ExecuteDataTable("ebook.SearchEbookPhanTrangDHY", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtEbook;
        }
        public DataTable GetTotalRecordSearchDHY(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int Status, long ToPicId, long SubjectId, string Op1 = "", string Op2 = "", string Op3 = "", string Op4 = "", string Op5 = "")
        {
            DataTable dtEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),

                new Framework.Database.DatabaseParamCls("status",Status),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("toPicId",ToPicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
                new Framework.Database.DatabaseParamCls("Op1",Op1),
                new Framework.Database.DatabaseParamCls("Op2",Op2),
                new Framework.Database.DatabaseParamCls("Op3",Op3),
                new Framework.Database.DatabaseParamCls("Op4",Op4),
                new Framework.Database.DatabaseParamCls("Op5",Op5),
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbook = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordSearchEbookDHY", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtEbook;
        }
        public DataTable GetTotalRecordSearch(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int Status, long ToPicId, long SubjectId,string TenTapChi="",string SoTapChi="")
        {
            DataTable dtEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("collectionId",CollectionId),
                new Framework.Database.DatabaseParamCls("UserId",UserId),
                 new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace( Title)),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace( Author)),
                new Framework.Database.DatabaseParamCls("Keyword",Framework.Environment.HtmlFunction.RemoteWhitSpace( Keyword)),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace( Publisher)),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace( PublishDate)),
                new Framework.Database.DatabaseParamCls("submitedFrom",SubmitedFrom),

                new Framework.Database.DatabaseParamCls("status",Status),
                new Framework.Database.DatabaseParamCls("submitedTo",SubmitedTo),
                new Framework.Database.DatabaseParamCls("toPicId",ToPicId),
                new Framework.Database.DatabaseParamCls("SubjectId",SubjectId),
         };
            if (!string.IsNullOrEmpty(TenTapChi))
            {
                Array.Resize(ref arrParams, arrParams.Length + 1);
                arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("TenTapChi", TenTapChi);
            }
            if (!string.IsNullOrEmpty(SoTapChi))
            {
                Array.Resize(ref arrParams, arrParams.Length + 1);
                arrParams[arrParams.Length - 1] = new Framework.Database.DatabaseParamCls("SoTapChi", SoTapChi);
            }

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbook = objSqlHelper.ExecuteDataTable("ebook.GetTotalRecordSearchEbook", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtEbook;
        }
        public DataTable ReportBienMucEbook(long UserId, string FromDate, string ToDate)
        {
            DataTable dtBienmuc = null;

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("UserId",UserId,"in"),
                new Framework.Database.DatabaseParamCls("submitedFrom",FromDate,"in"),
                new Framework.Database.DatabaseParamCls("submitedTo",ToDate,"in")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtBienmuc = objSqlHelper.ExecuteDataTable("ebook.ReportBiemMucEbook", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtBienmuc;

        }

        public string SaveEbook(Entities.Ebook.Ebook objEbook, long UserId)
        {
            String[] Output = new String[10];
            if (objEbook != null)
            {
                Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",objEbook.Id,"in"),
                new Framework.Database.DatabaseParamCls("title",Framework.Environment.HtmlFunction.RemoteWhitSpace(objEbook.Title),"in"),
                new Framework.Database.DatabaseParamCls("author",Framework.Environment.HtmlFunction.RemoteWhitSpace(objEbook.Author),"in"),
                new Framework.Database.DatabaseParamCls("brief",Framework.Environment.HtmlFunction.RemoteWhitSpace(objEbook.Brief),"in"),
                new Framework.Database.DatabaseParamCls("publisher",Framework.Environment.HtmlFunction.RemoteWhitSpace(objEbook.Publisher),"in"),
                new Framework.Database.DatabaseParamCls("publishDate",Framework.Environment.HtmlFunction.RemoteWhitSpace(objEbook.PublishDate),"in"),
                new Framework.Database.DatabaseParamCls("page",objEbook.Page,"in"),
                new Framework.Database.DatabaseParamCls("allowDownload",objEbook.AllowDownload,"in"),
                new Framework.Database.DatabaseParamCls("images",objEbook.Images,"in"),
                new Framework.Database.DatabaseParamCls("topicId",objEbook.TopicId,"in"),
                new Framework.Database.DatabaseParamCls("collectionId",objEbook.CollectionId,"in"),
                new Framework.Database.DatabaseParamCls("CreatedBy",UserId, "in"),
                new Framework.Database.DatabaseParamCls("UpdateBy",UserId,"in"),
                new Framework.Database.DatabaseParamCls("free",objEbook.Free,"in"),
                new Framework.Database.DatabaseParamCls("status",objEbook.Status,"in"),
                new Framework.Database.DatabaseParamCls("portalId",objEbook.PortalId,"in"),
                new Framework.Database.DatabaseParamCls("language",objEbook.Language,"in"),
                new Framework.Database.DatabaseParamCls("result","","return")
                };

                switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
                {
                    case "SQLSERVER":
                        {

                            Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.EditEbook", arrParams, CommandType.StoredProcedure);
                            break;
                        }
                }
                return Output[0].ToString();
            }
            else
            {
                return "";
            }
        }
        public void DeleteEbook(long Id)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",Id,"in")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        objSqlHelper.ExecuteQuery(null, "Ebook.DeleteEbook", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
        }
        public DataTable SearchSimpleEbook(string Keyword)
        {
            DataTable dtEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("Keyword",Keyword),
               
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbook = objSqlHelper.ExecuteDataTable("ebook.SearchBasicBookPhanTrang", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtEbook;
        }
        public DataTable SearchSimpleEbook(string Keyword, int ItemPerPage, int CurrentPage)
        {
            DataTable dtEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("Keyword",Keyword),
                new Framework.Database.DatabaseParamCls("itemperpage",ItemPerPage),
                new Framework.Database.DatabaseParamCls("currentPage",CurrentPage),
               
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbook = objSqlHelper.ExecuteDataTable("ebook.SearchEbookSimple", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtEbook;
        }
        public long GetTotalRecordSearchSimpleEbook(string Keyword)
        {

            DataTable dtEbook = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("value",Keyword),
               
         };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbook = objSqlHelper.ExecuteDataTable("ebook.GetRecordSearchBasicBook", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            if (Framework.Database.Table.CheckData(dtEbook))
            {

                return Convert.ToInt64(dtEbook.Rows[0][0].ToString());
            }
            else
            {
                return 0;
            }
        }
        public DataTable GetTopReadBook(int count)
        {
            DataTable dtTopBook = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtTopBook = objSqlHelper.ExecuteDataTable("Select top " + count + " ROW_NUMBER() OVER (ORDER BY totalView desc) AS [Index], totalView,title,author,publisher from Ebook.Item left outer join Ebook.itemXml on Ebook.Item.ID=Ebook.itemXml.ID order by totalView desc", CommandType.Text, null);
                        break;
                    }
            }
            return dtTopBook;
        }

        public void SaveUrlDownload(long EBookFileId, string url, string PortalId, long ReaderId, string CardNumber, string Ip)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("EBookFileId",EBookFileId,"in"),
                new Framework.Database.DatabaseParamCls("url",url,"in"),
                new Framework.Database.DatabaseParamCls("PortalId",PortalId,"in"),
                new Framework.Database.DatabaseParamCls("ReaderId",ReaderId,"in"),
                new Framework.Database.DatabaseParamCls("CardNumber",CardNumber,"in"),
                new Framework.Database.DatabaseParamCls("Ip",Ip,"in"),
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        objSqlHelper.ExecuteQuery(null, "Ebook.SaveUrlDownload", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }

        }
        public DataTable GetTopDownloadBook(int count)
        {
            DataTable dtTopBook = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtTopBook = objSqlHelper.ExecuteDataTable("Select top " + count + " ROW_NUMBER() OVER (ORDER BY totalView desc) AS [Index], totalDownload,title,author,publisher from Ebook.Item left outer join Ebook.itemXml on Ebook.Item.ID=Ebook.itemXml.ID where totalDownload>0 order by totalDownload desc", CommandType.Text, null);
                        break;
                    }
            }
            return dtTopBook;
        }

        public DataTable checkSavedBook(long usID, long ebID)
        {
            DataTable dtTopBook = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtTopBook = objSqlHelper.ExecuteDataTable("Select * from Ebook.savedBook where userID=" + usID + " and bookID=" + ebID, CommandType.Text, null);
                        break;
                    }
            }
            return dtTopBook;
        }

        public DataTable ListSavedBook(int p)
        {
            DataTable dtTopBook = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                            new Framework.Database.DatabaseParamCls("userID",p,"in"),
                         };
                        dtTopBook = objSqlHelper.ExecuteDataTable("[Ebook].[ListSavedBook]", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtTopBook;
        }

        public DataTable ListNewEBookInCollection(string p1, int p2)
        {
            DataTable dtTopBook = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                            new Framework.Database.DatabaseParamCls("top",p1,"in"),
                            new Framework.Database.DatabaseParamCls("collection",p2,"in"),
                         };
                        dtTopBook = objSqlHelper.ExecuteDataTable("Ebook.ListNewBookInCollection", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtTopBook;
        }
        public DataTable ReportEbookViewAndDownload(string Title, string Author, string Publisher, string PublishDate, string Keyword, string StartTime, string EndTime, int Top)
        {
            DataTable dtTopBook = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                            new Framework.Database.DatabaseParamCls("Title",Title),
                            new Framework.Database.DatabaseParamCls("Author",Author),
                            new Framework.Database.DatabaseParamCls("Publisher",Publisher),
                            new Framework.Database.DatabaseParamCls("PublishDate",PublishDate),
                            new Framework.Database.DatabaseParamCls("Keyword",Keyword),
                            new Framework.Database.DatabaseParamCls("StartTime",StartTime),
                            new Framework.Database.DatabaseParamCls("EndTime",EndTime),
                            new Framework.Database.DatabaseParamCls("Top",Top),
                            
                         };
                        dtTopBook = objSqlHelper.ExecuteDataTable("Ebook.ReportEbookViewAndDownload", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtTopBook;
        }
        public DataTable ReportEbookNotView( string StartTime, string EndTime,int Type)
        {
            DataTable dtTopBook = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                            
                            new Framework.Database.DatabaseParamCls("StartTime",StartTime),
                            new Framework.Database.DatabaseParamCls("EndTime",EndTime),
                            new Framework.Database.DatabaseParamCls("Type",Type)
                            
                         };
                        dtTopBook = objSqlHelper.ExecuteDataTable("Ebook.SearchEbookNotView", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtTopBook;
        }
        public long GetNumberEbookViewAndDownload(long EbookId, string StartTime, string EndTime, int Type)
        {
            DataTable dtTopBook = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                            new Framework.Database.DatabaseParamCls("EbookId",EbookId),
                            
                            new Framework.Database.DatabaseParamCls("StartTime",StartTime),
                            new Framework.Database.DatabaseParamCls("EndTime",EndTime),
                            new Framework.Database.DatabaseParamCls("Type",Type),
                            
                         };
                        dtTopBook = objSqlHelper.ExecuteDataTable("Ebook.GetNumberEbookViewAndDownload", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            long iTotal = 0;
            try
            {
                iTotal = Convert.ToInt16(dtTopBook.Rows[0][0].ToString());
            }
            catch
            {
            }
            return iTotal;
        }
        public void UpdateCoverImage(long EbookId, string Images)
        {
            Framework.Database.DatabaseParamCls[] arraParams = new Framework.Database.DatabaseParamCls[]
                {
               
                new Framework.Database.DatabaseParamCls("Id",EbookId),
               new Framework.Database.DatabaseParamCls("Images",Images)
               
                };

            objSqlHelper.ExecuteQuery(null, "Update Ebook.Item set Images=@Images where Id=@Id", arraParams, CommandType.Text);
        }
        public long CountEbookOfTopic(long EbookId)
        {
            DataTable dtEbookFile = null;
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("EbookId",EbookId,"in"),
                
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtEbookFile = objSqlHelper.ExecuteDataTable("Ebook.CountEbookOfTopic", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }

            return Convert.ToInt64(dtEbookFile.Rows[0][0].ToString());

        }
    }
}
