using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
namespace DataAccess.Ebook
{
  public  class EbookItem
    {
      Framework.Database.SqlDatabaseHelper objSqlHelper = null;
      public EbookItem()
      {
          objSqlHelper = new Framework.Database.SqlDatabaseHelper();
      }
      public EbookItem(string ConnectionString)
      {
          objSqlHelper = new Framework.Database.SqlDatabaseHelper(ConnectionString);
      }
      public long GetEbookItemCloud(long ItemId)
      {
          string sSQL = "Select Id from Ebook.Item where OrgEbookId=" + ItemId.ToString();
          DataTable dt = objSqlHelper.ExecuteDataTable(sSQL);
          long iResult = 0;
          if (Framework.Database.Table.CheckData(dt))
          {
              try
              {
                  iResult = Convert.ToInt64(dt.Rows[0]["Id"].ToString());
              }
              catch
              {

              }
          }
          return iResult;
      }
      public void UpdateAuthorId(long ItemId,string AuthorId,long MagazineId,string OrgId="")
      {
          String[] Output = new String[10];

          Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("ItemId",ItemId,"in"),
                new Framework.Database.DatabaseParamCls("AuthorId",AuthorId,"in"),
                new Framework.Database.DatabaseParamCls("MagazineId",MagazineId,"in"),
                new Framework.Database.DatabaseParamCls("OrgId",OrgId,"in")
            };

          switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
          {
              case "SQLSERVER":
                  {

                      Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.UpdateAuthorId", arrParams, CommandType.StoredProcedure);
                      break;
                  }
          }
      }
      public void SaveLogBienMucEbook(long EbookId, long UserId, string Status)
      {
            String[] Output = new String[10];

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("UserId",UserId,"in"),
                new Framework.Database.DatabaseParamCls("DigId",EbookId,"in"),
                new Framework.Database.DatabaseParamCls("status",Status,"in")
                
            };

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.SaveLogBienMucEbook", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }

      }
      /// <summary>
      /// Lưu dữ liệu thông tin cơ bản của tài liệu Ebook chung
      /// </summary>
      /// <param name="objEBookItem"></param>
      /// <returns></returns>
       public string SaveBookItemCloud(Entities.Ebook.EbookItem objEBookItem)
        {

            String[] Output = new String[10];

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",objEBookItem.Id,"in"),
                new Framework.Database.DatabaseParamCls("collectionId",objEBookItem.CollectionId,"in"),
                new Framework.Database.DatabaseParamCls("SubjectId",objEBookItem.SubjectId,"in"),
                new Framework.Database.DatabaseParamCls("createdBy",objEBookItem.CreatedBy,"in"),
                new Framework.Database.DatabaseParamCls("status",objEBookItem.Status,"in"),
                new Framework.Database.DatabaseParamCls("TypeId",objEBookItem.TypeId,"in"),
                new Framework.Database.DatabaseParamCls("UpdateBy",objEBookItem.UpdatedBy,"in"),
                new Framework.Database.DatabaseParamCls("AllowDownload",objEBookItem.AllowDownload,"in"),
                new Framework.Database.DatabaseParamCls("images",objEBookItem.Images,"in"),
                new Framework.Database.DatabaseParamCls("free",objEBookItem.Free,"in"),
                new Framework.Database.DatabaseParamCls("ToPicId",objEBookItem.TopicId,"in"),
                new Framework.Database.DatabaseParamCls("ProjectId",objEBookItem.ProjectId,"in"),
            new Framework.Database.DatabaseParamCls("OrgId",objEBookItem.OrgId,"in"),
            new Framework.Database.DatabaseParamCls("OrgEbookId",objEBookItem.OrgEbookId,"in"),
                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.EditEbookItem", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
            return Output[0].ToString();


        }
        public string SaveBookItem(Entities.Ebook.EbookItem objEBookItem)
        {

            String[] Output = new String[10];

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",objEBookItem.Id,"in"),
                new Framework.Database.DatabaseParamCls("collectionId",objEBookItem.CollectionId,"in"),
                new Framework.Database.DatabaseParamCls("SubjectId",objEBookItem.SubjectId,"in"),
                new Framework.Database.DatabaseParamCls("createdBy",objEBookItem.CreatedBy,"in"),
                new Framework.Database.DatabaseParamCls("status",objEBookItem.Status,"in"),
                new Framework.Database.DatabaseParamCls("TypeId",objEBookItem.TypeId,"in"),
                new Framework.Database.DatabaseParamCls("UpdateBy",objEBookItem.UpdatedBy,"in"),
                new Framework.Database.DatabaseParamCls("AllowDownload",objEBookItem.AllowDownload,"in"),
                new Framework.Database.DatabaseParamCls("images",objEBookItem.Images,"in"),
                new Framework.Database.DatabaseParamCls("free",objEBookItem.Free,"in"),
                new Framework.Database.DatabaseParamCls("ToPicId",objEBookItem.TopicId,"in"),
                new Framework.Database.DatabaseParamCls("ProjectId",objEBookItem.ProjectId,"in"),
                new Framework.Database.DatabaseParamCls("Share",objEBookItem.Share,"in"),
                new Framework.Database.DatabaseParamCls("ParentId",objEBookItem.ParentId,"in"),

                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.EditEbookItem", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
            return Output[0].ToString();


        }

        public string SaveBookItemT36(Entities.Ebook.EbookItemT36 objEBookItem)
        {

            String[] Output = new String[10];

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",objEBookItem.Id,"in"),
                new Framework.Database.DatabaseParamCls("collectionId",objEBookItem.CollectionId,"in"),
                new Framework.Database.DatabaseParamCls("SubjectId",objEBookItem.SubjectId,"in"),
                new Framework.Database.DatabaseParamCls("createdBy",objEBookItem.CreatedBy,"in"),
                new Framework.Database.DatabaseParamCls("status",objEBookItem.Status,"in"),
                new Framework.Database.DatabaseParamCls("TypeId",objEBookItem.TypeId,"in"),
                new Framework.Database.DatabaseParamCls("UpdateBy",objEBookItem.UpdatedBy,"in"),
                new Framework.Database.DatabaseParamCls("AllowDownload",objEBookItem.AllowDownload,"in"),
                new Framework.Database.DatabaseParamCls("images",objEBookItem.Images,"in"),
                new Framework.Database.DatabaseParamCls("free",objEBookItem.Free,"in"),
                new Framework.Database.DatabaseParamCls("ToPicId",objEBookItem.TopicId,"in"),
                new Framework.Database.DatabaseParamCls("ProjectId",objEBookItem.ProjectId,"in"),
                new Framework.Database.DatabaseParamCls("Share",objEBookItem.Share,"in"),
                new Framework.Database.DatabaseParamCls("DoMat",objEBookItem.DoMat,"in"),
                new Framework.Database.DatabaseParamCls("ParentId",objEBookItem.ParentId,"in"),

                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.EditEbookItem", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
            return Output[0].ToString();


        }
        public string SaveBookItemLRC(Entities.Ebook.EbookItem objEBookItem)
        {

            String[] Output = new String[10];

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",objEBookItem.Id,"in"),
                new Framework.Database.DatabaseParamCls("collectionId",objEBookItem.CollectionId,"in"),
                new Framework.Database.DatabaseParamCls("SubjectId",objEBookItem.SubjectId,"in"),
                new Framework.Database.DatabaseParamCls("createdBy",objEBookItem.CreatedBy,"in"),
                new Framework.Database.DatabaseParamCls("status",objEBookItem.Status,"in"),
                new Framework.Database.DatabaseParamCls("TypeId",objEBookItem.TypeId,"in"),
                new Framework.Database.DatabaseParamCls("UpdateBy",objEBookItem.UpdatedBy,"in"),
                new Framework.Database.DatabaseParamCls("AllowDownload",objEBookItem.AllowDownload,"in"),
                new Framework.Database.DatabaseParamCls("images",objEBookItem.Images,"in"),
                new Framework.Database.DatabaseParamCls("free",objEBookItem.Free,"in"),
                new Framework.Database.DatabaseParamCls("ToPicId",objEBookItem.TopicId,"in"),
                new Framework.Database.DatabaseParamCls("GroupToPic",objEBookItem.GroupTopic,"in"),
                new Framework.Database.DatabaseParamCls("ProjectId",objEBookItem.ProjectId,"in"),
                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.EditEbookItem", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
            return Output[0].ToString();


        }
      /// <summary>
      /// Lưu dữ liệu thông tin cơ bản của tài liệu verson cơ sở dữ liệu bài báo
      /// </summary>
      /// <param name="objEBookItem"></param>
      /// <returns></returns>
        public string SaveBookItem(Entities.Article.EbookItem objEBookItem)
        {

            String[] Output = new String[10];

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",objEBookItem.Id,"in"),
                new Framework.Database.DatabaseParamCls("collectionId",objEBookItem.CollectionId,"in"),
                new Framework.Database.DatabaseParamCls("SubjectId",objEBookItem.SubjectId,"in"),
                new Framework.Database.DatabaseParamCls("createdBy",objEBookItem.CreatedBy,"in"),
                new Framework.Database.DatabaseParamCls("status",objEBookItem.Status,"in"),
                new Framework.Database.DatabaseParamCls("TypeId",objEBookItem.TypeId,"in"),
                new Framework.Database.DatabaseParamCls("UpdateBy",objEBookItem.UpdatedBy,"in"),
                new Framework.Database.DatabaseParamCls("AllowDownload",objEBookItem.AllowDownload,"in"),
                new Framework.Database.DatabaseParamCls("images",objEBookItem.Images,"in"),
                new Framework.Database.DatabaseParamCls("free",objEBookItem.Free,"in"),
                new Framework.Database.DatabaseParamCls("ToPicId",objEBookItem.TopicId,"in"),
                new Framework.Database.DatabaseParamCls("AuthorId",objEBookItem.AuthorId,"in"),
                new Framework.Database.DatabaseParamCls("MagazineId",objEBookItem.MagazineId,"in"),
                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.EditEbookItem", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
            return Output[0].ToString();


        }
        public string SaveBookItem(Entities.Magazine.EbookItem objEBookItem)
        {

            String[] Output = new String[10];

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",objEBookItem.Id,"in"),
                new Framework.Database.DatabaseParamCls("collectionId",objEBookItem.CollectionId,"in"),
                new Framework.Database.DatabaseParamCls("SubjectId",objEBookItem.SubjectId,"in"),
                new Framework.Database.DatabaseParamCls("createdBy",objEBookItem.CreatedBy,"in"),
                new Framework.Database.DatabaseParamCls("status",objEBookItem.Status,"in"),
                new Framework.Database.DatabaseParamCls("TypeId",objEBookItem.TypeId,"in"),
                new Framework.Database.DatabaseParamCls("UpdateBy",objEBookItem.UpdatedBy,"in"),
                new Framework.Database.DatabaseParamCls("AllowDownload",objEBookItem.AllowDownload,"in"),
                new Framework.Database.DatabaseParamCls("images",objEBookItem.Images,"in"),
                new Framework.Database.DatabaseParamCls("free",objEBookItem.Free,"in"),
                new Framework.Database.DatabaseParamCls("ToPicId",objEBookItem.TopicId,"in"),
                
                new Framework.Database.DatabaseParamCls("SoTapChiId",objEBookItem.SoTapChiId,"in"),
                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.EditEbookItem", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
            return Output[0].ToString();


        }
        public void UpdateAuthorAndKeyword(long ItemId)
        {
             Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("itemId",ItemId,"in"),
                
                new Framework.Database.DatabaseParamCls("result","","return")
             };
             switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
             {
                 case "SQLSERVER":
                     {
                         objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.UpdateAuthorAndKeyword", arrParams, CommandType.StoredProcedure);
                         break;
                     }
             }
        }
        public void UpdateDeleteCloud(long ItemId)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("itemId",ItemId,"in"),
                
                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.UpdateDeleteCloud", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
        }
    }
}
