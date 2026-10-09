using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
namespace Bussiness.Ebook
{
    public class EbookItem
    {

        DataAccess.Ebook.EbookItem objDEbookItem = null;
        public EbookItem()
        {
            objDEbookItem = new DataAccess.Ebook.EbookItem();
        }
        public EbookItem(string ConnectionString)
        {
            objDEbookItem = new DataAccess.Ebook.EbookItem(ConnectionString);
        }
        public void UpdateAuthorId(long ItemId, string AuthorId,long MagazineId)
        {

           
                    string[] AuthorIds = AuthorId.Split(',');
                    string strOrgId = "";
                    foreach (string s in AuthorIds)
                    {
                        if (s != "")
                        {
                            long lnAuthorId = 0;
                            try
                            {
                                lnAuthorId = Convert.ToInt64(s);

                            }
                            catch
                            {
                            }
                            if (lnAuthorId != 0)
                            {
                                Bussiness.Article.DicAuthor objBDicAuthor = new Bussiness.Article.DicAuthor();
                                DataTable temp = objBDicAuthor.GetDataTableDicAuthorById(lnAuthorId);
                                if (temp.Rows.Count > 0)
                                {
                                    if (strOrgId != "")
                                    {
                                        strOrgId = strOrgId + "," + temp.Rows[0]["OrgId"].ToString();
                                    }
                                    else
                                    {
                                        strOrgId = strOrgId +  temp.Rows[0]["OrgId"].ToString();
                                    }
                                }
                            }
                        }
                    }

            objDEbookItem.UpdateAuthorId(ItemId, AuthorId,MagazineId,strOrgId);
        }
        public string SaveEbookItemLRC(Entities.Ebook.EbookItem objEbookItem, int iCloud = 0)
        {
            string Output = objDEbookItem.SaveBookItemLRC(objEbookItem);
            if (iCloud > 0)
            {
                Bussiness.Common.Org objBOrg = new Common.Org(Framework.Environment.Portal._cloudElibConnectionString);

                DataAccess.Ebook.EbookItem objDEbookItemCloud = new DataAccess.Ebook.EbookItem(Framework.Environment.Portal._cloudElibConnectionString);
                if (objEbookItem.Id == 0)
                {
                    objEbookItem.OrgEbookId = Convert.ToInt64(Output.ToString());
                }
                DataTable temp = objBOrg.GetOrgByCode(Framework.Environment.Portal.Id);
                long iOrgId = 0;
                if (Framework.Database.Table.CheckData(temp))
                {
                    try
                    {
                        iOrgId = Convert.ToInt64(temp.Rows[0]["Id"].ToString());
                    }
                    catch
                    {
                    }
                }
                objEbookItem.OrgId = iOrgId;
                objEbookItem.TopicId = 0;
                objEbookItem.CollectionId = 0;
                objEbookItem.SubjectId = 0;
                string sOut = "";
                try
                {
                    sOut = objDEbookItemCloud.SaveBookItemCloud(objEbookItem);
                }
                catch
                {
                    sOut = "0";
                }

                Output = Output + "," + sOut;
            }

            if (objEbookItem.Id == 0)
            {
                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(string.Format(Framework.Environment.Language.GetLanguage("Add new ebook ID : {0}", "Thêm mới tài liệu ID : {0}"), Output), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBookItem", "Add"));
            }
            else
            {
                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(string.Format(Framework.Environment.Language.GetLanguage("Edit ebook ID : {0}", "Chỉnh sửa tài liệu ID : {0}"), Output), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBookItem", "Edit"));
            }
            return Output;
        }
        public long GetEbookItemCloud(long ItemId)
        {
            DataAccess.Ebook.EbookItem objDEbookItemCloud = new DataAccess.Ebook.EbookItem(Framework.Environment.Portal._cloudElibConnectionString);
            return objDEbookItemCloud.GetEbookItemCloud(ItemId);
        }
        public string SaveEbookItem(Entities.Ebook.EbookItem objEbookItem, int iCloud=0)
        {
            string Output = objDEbookItem.SaveBookItem(objEbookItem);
            if (iCloud > 0)
            {
                Bussiness.Common.Org objBOrg = new Common.Org(Framework.Environment.Portal._cloudElibConnectionString);
                DataAccess.Ebook.EbookItem objDEbookItemCloud = new DataAccess.Ebook.EbookItem(Framework.Environment.Portal._cloudElibConnectionString);
                if (objEbookItem.Id == 0)
                {
                    objEbookItem.OrgEbookId = Convert.ToInt64(Output.ToString());
                }
                DataTable temp = objBOrg.GetOrgByCode(Framework.Environment.Portal.Id);
                long iOrgId = 0;
                if (Framework.Database.Table.CheckData(temp))
                {
                    try
                    {
                        iOrgId = Convert.ToInt64(temp.Rows[0]["Id"].ToString());
                    }
                    catch
                    {
                    }
                }
                objEbookItem.OrgId = iOrgId;
                objEbookItem.TopicId = 0;
                objEbookItem.CollectionId = 0;
                objEbookItem.SubjectId = 0;

                if (objEbookItem.Id > 0)
                {
                    long iItemCloudId = GetEbookItemCloud(objEbookItem.Id);
                    
                    if (iItemCloudId > 0)
                    {
                        objEbookItem.Id = iItemCloudId;
                        string sOut = "";
                        try
                        {
                            sOut = objDEbookItemCloud.SaveBookItemCloud(objEbookItem);
                        }
                        catch
                        {
                            sOut = "0";
                        }

                        Output = Output + "," + sOut;
                    }
                    else
                    {
                        //Truong hop chua ton tai ban ghi tren Cloud
                        Bussiness.Ebook.EbookCloud objbBEbookCloud = new EbookCloud();
                        objbBEbookCloud.ConvertEbookItem(objEbookItem.Id, objEbookItem.Id,Framework.Environment.Portal.Id);
                    }
                }
                else
                {
                   //Them moi
                    string sOut = "";
                    try
                    {
                        sOut = objDEbookItemCloud.SaveBookItemCloud(objEbookItem);
                    }
                    catch
                    {
                        sOut = "0";
                    }

                    Output = Output + "," + sOut;
                }
               
            }
            
            if (objEbookItem.Id == 0)
            {
                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(string.Format(Framework.Environment.Language.GetLanguage("Add new ebook ID : {0}", "Thêm mới tài liệu ID : {0}"), Output), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBookItem", "Add"));
            }
            else
            {
                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(string.Format(Framework.Environment.Language.GetLanguage("Edit ebook ID : {0}", "Chỉnh sửa tài liệu ID : {0}"), Output), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBookItem", "Edit"));
            }
            return Output;
        }

        public string SaveEbookItemT36(Entities.Ebook.EbookItemT36 objEbookItem, int iCloud = 0)
        {
            string Output = objDEbookItem.SaveBookItemT36(objEbookItem);
            

            if (objEbookItem.Id == 0)
            {
                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(string.Format(Framework.Environment.Language.GetLanguage("Add new ebook ID : {0}", "Thêm mới tài liệu ID : {0}"), Output), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBookItem", "Add"));
            }
            else
            {
                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(string.Format(Framework.Environment.Language.GetLanguage("Edit ebook ID : {0}", "Chỉnh sửa tài liệu ID : {0}"), Output), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBookItem", "Edit"));
            }
            return Output;
        }
        public string SaveEbookItem(Entities.Magazine.EbookItem objEbookItem)
        {
            string Output = objDEbookItem.SaveBookItem(objEbookItem);
            if (objEbookItem.Id == 0)
            {
                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(string.Format(Framework.Environment.Language.GetLanguage("Add new ebook ID : {0}", "Thêm mới tài liệu ID : {0}"), Output), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBookItem", "Add"));
            }
            else
            {
                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(string.Format(Framework.Environment.Language.GetLanguage("Edit ebook ID : {0}", "Chỉnh sửa tài liệu ID : {0}"), Output), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBookItem", "Edit"));
            }
            return Output;
        }
        public void SaveLogBienMucEbook(long EbookId, long UserId, string Status)
        {
            objDEbookItem.SaveLogBienMucEbook(EbookId, UserId, Status);

        }
        public void UpdateAuthorAndKeyword(long ItemId,int iCloud=0)
        {
            objDEbookItem.UpdateAuthorAndKeyword(ItemId);
            

        }
        public void UpdateDeleteCloud(long ItemId)
        {
            objDEbookItem.UpdateDeleteCloud(ItemId);
        }
        public void DeleteEbookItem(long Id)
        {
            Bussiness.Ebook.EbookFile objEbookFile = new EbookFile();
            DataTable dtFile = objEbookFile.SearchEbookFile("", Id);
            if (Framework.Database.Table.CheckData(dtFile))
            {
                foreach (DataRow row in dtFile.Rows)
                {
                    string url = row["url"].ToString();
                    Framework.Web.File.DeleteFile(Framework.Web.File.GetPhysipicPath("/" + url));
                }
            }
            Bussiness.Ebook.Ebook objBEbook = new Ebook();
            objBEbook.DeleteEbook(Id);

            string sCloud = Framework.Environment.Setting.GetKey("SaveCloud");
            int iCloud = 0;
            try
            {
                iCloud = Convert.ToInt16(sCloud);
            }
            catch
            {

            }
            if (iCloud > 0)
            {
                if (Framework.Environment.Setting.GetKey("SysCloud") == "2")
                {
                    //Dong Bo voi Cloud
                    long iItemCloudId = GetEbookItemCloud(Id);
                    if (iItemCloudId > 0)
                    {
                        Bussiness.Ebook.EbookFile objEbookFileCloud = new EbookFile(Framework.Environment.Portal._cloudElibConnectionString);
                        DataTable dtFileCloud = objEbookFileCloud.SearchEbookFile("", iItemCloudId);
                        //if (Framework.Database.Table.CheckData(dtFileCloud))
                        //{
                        //    foreach (DataRow row in dtFileCloud.Rows)
                        //    {
                        //        string url = row["url"].ToString();
                        //        Framework.Web.File.DeleteFile(Framework.Web.File.GetPhysipicPath("/" + url));
                        //    }
                        //}
                        Bussiness.Ebook.Ebook objBEbookCloud = new Ebook(Framework.Environment.Portal._cloudElibConnectionString);
                        objBEbookCloud.DeleteEbook(iItemCloudId);
                    }
                }
                else
                {
                    long iItemCloudId = GetEbookItemCloud(Id);
                    if (iItemCloudId > 0)
                    {
                        UpdateDeleteCloud(iItemCloudId);
                    }
                }
            }


            Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(string.Format(Framework.Environment.Language.GetLanguage("Delete ebook ID : {0}", "Xóa tài liệu ID : {0}"), Id), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBookItem", "Delete"));
        }
    }
}
