using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
namespace Bussiness.Ebook
{
    public class Ebook
    {
        public DataAccess.Ebook.Ebook objDEbook;

        public Ebook()
        {
            objDEbook = new DataAccess.Ebook.Ebook();
        }

        public Ebook(string ConnectionString)
        {
            objDEbook = new DataAccess.Ebook.Ebook(ConnectionString);
        }
        /// <summary>
        /// Hiển thị hình ảnh sách dựa vào ID và Bộ sưu tập CollectionID của sách
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="CollectionId"></param>
        /// <returns></returns>
        public string DisplayEbookImage(string Id, string CollectionId,string Images)
        {
            string image = "";

            image = Images;

            if (image != "")
                {
                    if (!Framework.Environment.File.Exists(Framework.Environment.File.GetPhysipicPath(image)))
                    {
                       
                        if (image.Substring(0, 1) != "/")
                        {
                            image = "/" + image;
                        }
                    }
                    else
                    {
                        image = GetImageByCollection(CollectionId);
                    }

                }
                else
                {

                    image = GetImageByCollection(CollectionId);
                }
           

           


            return image;


        }
        public string GetTitle(long Id)
        {
            DataTable dt = objDEbook.GetTitle(Id);
            if (Framework.Database.Table.CheckData(dt))
            {
                return dt.Rows[0]["Title"].ToString();
            }
            else
            {
                return "";
            }    
        }
        public string GetImageByCollection(string CollectionId)
        {
            String image = "";
            switch (CollectionId)
            {
                case "1":
                    {
                        image = "/themes/LRCEbook/images/luan van luan an.jpg";
                        break;
                    }
                case "9":
                    {
                        image = "/themes/LRCEbook/images/khoa hoc cong nghe .jpg";
                        break;
                    }
                case "17":
                    {
                        image = "/themes/LRCEbook/images/sach tieng anh.jpg";
                        break;
                    }
                case "2":
                    {
                        image = "/themes/LRCEbook/images/bai giang.jpg";
                        break;
                    }
                case "16":
                    {
                        image = "/themes/LRCEbook/images/ket qua nghien cuu.jpg";
                        break;
                    }
                case "20":
                    {
                        image = "/themes/LRCEbook/images/tham khao.jpg";
                        break;
                    }
                case "10":
                    {
                        image = "/themes/LRCEbook/images/video_icon_1.jpg";
                        break;
                    }
                case "13":
                    {
                        image = "/themes/LRCEbook/images/app document management.png";
                        image = "/themes/LRCEbook/images/documents.jpg";

                        break;
                    }
                default:
                    {
                        image = "/themes/LRCEbook/images/Book lrc.jpg";
                        break;
                    }

            }
            return image;
        }

        public DataTable BrowseBy(string By, string Value)
        {
            DataTable dtEbook = objDEbook.BrowseBy(By, Value);
            dtEbook = ProcessImageAndUrl(dtEbook);
            return dtEbook;
        }
        public void UpdateCollection(long Id, long CollectionId, long SubjectId, long ToPicId)
        {
            objDEbook.UpdateCollection(Id, CollectionId, SubjectId, ToPicId);
        }
        public void UpdateTotaDownload(long Id)
        {
            objDEbook.UpdateTotalDownload(Id);
        }
        public void UpdateTotalView(long Id)
        {
            objDEbook.UpdateTotalView(Id);
        }
        /// <summary>
        /// Lấy danh sách tài liệu nổi bật là những tài liệu có lượt xem nhiều
        /// </summary>
        /// <returns></returns>
        public DataTable ListTaiLieuNoiBat(int top)
        {
            return objDEbook.ListTaiLieuNoiBat(top);
        }
        public void UpdateCoverBook(long EbookId, string Images)
        {
            objDEbook.UpdateCoverImage(EbookId, Images);
           
        }

        public DataTable ListNewEBook(string NumberEbook)
        {
            DataTable dtBBook = objDEbook.ListNewEBook(NumberEbook);
            dtBBook = ProcessImageAndUrl(dtBBook);
            return dtBBook;
        }
        public DataTable ListNamTapChi(string NumberEbook,string Collection="")
        {
            DataTable dtBBook = objDEbook.ListNamTapChi(NumberEbook, Collection);
            
            return dtBBook;
        }


        public string GetDigType(long digTypeId)
        {
            DataTable dt =objDEbook. GetDigType(digTypeId);
            string sOut = "";
            if (Framework.Database.Table.CheckData(dt))
            {
                sOut = dt.Rows[0]["Code"].ToString();
            }
            else
            {
                sOut = "Book";
            }
            return sOut;
        }
        public DataTable ListNewPrintBook(string NumberPBook)
        {
            DataTable dtBBook = objDEbook.ListNewPBook(NumberPBook);
            dtBBook = ProcessImageAndUrl(dtBBook);
            return dtBBook;
        }

        public DataTable ListSachLienQuan(string NumberEbook, int collection)
        {
            DataTable dtBBook = objDEbook.ListTaiLieuLienQuan(NumberEbook, collection);
            dtBBook = ProcessImageAndUrl(dtBBook);
            return dtBBook;
        }
        public bool checkAllowDownload(long Id)
        {
            DataTable dt = GetDataTableEbookById(Id);
            if (dt.Rows.Count > 0)
            {
                if (dt.Rows[0]["AllowDownload"].ToString() == "1" || dt.Rows[0]["AllowDownload"] == null)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }
        public DataTable GetDataTableEbookById(long Id)
        {
            return objDEbook.GetDataTableEbookById(Id);
        }
        public DataTable checkSavedBook(long usID, long ebID)
        {
            return objDEbook.checkSavedBook(usID, ebID);
        }
        public void ChangeEbookStatus(long Id, int Status)
        {
            DataTable dt = GetDataTableEbookById(Id);
            if (Framework.Database.Table.CheckData(dt))
            {
                try
                {
                    Status = Convert.ToInt16(dt.Rows[0]["status"]);
                }
                catch
                {

                }
            }
            objDEbook.ChangeEbookStatus(Id, Status);
            Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(Framework.Environment.Language.GetLanguage("Change ebook status ID : ", "Thay đổi trạng thái tài liệu ID : ") + Id.ToString(), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBook", "Edit"));
        }
        public void ChangeEbookShow(long Id, int Status)
        {
            objDEbook.ChangeEbookShow(Id, Status);
            Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog(Framework.Environment.Language.GetLanguage("Change ebook display status ID : ", "Thay đổi trạng thái hiển thị tài liệu ID : ") + Id.ToString(), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBook", "Edit"));
        }
        public DataTable SearchEbookTapChiChanNuoi(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", int ItemPerPage = 0, int CurrentPage = 0, string TenTapChi = "", string SoTapChi = "", long  MagazineId=0, long SoTapChiId=0,string NgayPhatHanhFrom="",string NgayPhatHanhTo="")
        {
           
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);
            DataTable dtEbook = objDEbook.SearchEbookTapChiChanNuoi(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, Id, Top, OrderBy, PortalId, Language, ItemPerPage, CurrentPage, TenTapChi, SoTapChi,MagazineId,SoTapChiId);
            if (Framework.Database.Table.CheckData(dtEbook))
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    if (row["images"].ToString() == "")
                    {
                       
                        if (! string.IsNullOrEmpty( row["MagazineImages"].ToString()))
                        {
                            row["images"] = row["MagazineImages"].ToString();
                        }
                        else
                        {
                            row["images"] = "/images/Ebook.png";
                        }
                    }
                    else
                    {
                        
                        if (row["images"].ToString().Substring(0, 1) != "/")
                        {
                            row["images"] = "/" + row["images"].ToString();
                        }



                      
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                    //KTCN
                    //if (Framework.Environment.Portal.rewrite == "1")
                    //{
                    //    row["url"] = "/Xem/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    //}
                    //else
                    //{
                    //    row["url"] = "/Default.aspx?page=View&SubId=" + row["id"].ToString();
                    //}
                }
            }
            return dtEbook;
        }
        public DataTable SearchEbookBaoND(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", int ItemPerPage = 0, int CurrentPage = 0, string TenTapChi = "", string SoTapChi = "", long MagazineId = 0, long SoTapChiId = 0, string NgayPhatHanhFrom = "", string NgayPhatHanhTo = "")
        {

            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);
            DataTable dtEbook = objDEbook.SearchEbookBaoND(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, Id, Top, OrderBy, PortalId, Language, ItemPerPage, CurrentPage, TenTapChi, SoTapChi, MagazineId, SoTapChiId,NgayPhatHanhFrom,NgayPhatHanhTo);
            if (Framework.Database.Table.CheckData(dtEbook))
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    if (row["images"].ToString() == "")
                    {

                        if (!string.IsNullOrEmpty(row["MagazineImages"].ToString()))
                        {
                            row["images"] = row["MagazineImages"].ToString();
                        }
                        else
                        {
                            row["images"] = "/images/Ebook.png";
                        }
                    }
                    else
                    {

                        if (row["images"].ToString().Substring(0, 1) != "/")
                        {
                            row["images"] = "/" + row["images"].ToString();
                        }




                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                    //KTCN
                    //if (Framework.Environment.Portal.rewrite == "1")
                    //{
                    //    row["url"] = "/Xem/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    //}
                    //else
                    //{
                    //    row["url"] = "/Default.aspx?page=View&SubId=" + row["id"].ToString();
                    //}
                }
            }
            return dtEbook;
        }
        public string GetNguoiHD(long itemId)
        {
            Bussiness.Ebook.MetaDataValue objBMetaDataValue = new MetaDataValue();
           DataTable dt= objBMetaDataValue.GetMetdataFieldValue(itemId, 2);
            string sOut = "";
            if (Framework.Database.Table.CheckData(dt))
            {
                foreach(DataRow row in dt.Rows)
                {
                    if (string.IsNullOrEmpty(sOut))
                    {
                        sOut = sOut + row["Value"].ToString();
                    }
                    else
                    {
                        sOut = sOut + ","+ row["Value"].ToString();
                    }    
                }    
            }
            return sOut;
        }
        public DataTable SearchEbook(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId,string publishDateFrom="",string publishDateTo="", long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", long ItemPerPage = 0, long CurrentPage = 0,string TenTapChi="", string SoTapChi="")
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);
            DataTable dtEbook = objDEbook.SearchEbook(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId,publishDateFrom,publishDateTo, Id, Top, OrderBy, PortalId, Language, ItemPerPage, CurrentPage,TenTapChi,SoTapChi);
            if (Framework.Database.Table.CheckData(dtEbook))
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    row["NguoiHd"] = GetNguoiHD(Convert.ToInt64(row["Id"]));

                    if (row["images"].ToString() == "")
                    {
                        row["images"] = "/images/EBook.png";
                    }
                    else
                    {
                        //if (!Framework.Environment.File.Exists(Framework.Web.File.GetPhysipicPath(row["images"].ToString())))
                        //{
                        //    row["images"] = "/images/EBook.png";
                        //}
                        //else
                        //{
                        if (row["images"].ToString().Substring(0, 1) != "/")
                        {
                            row["images"] = "/" + row["images"].ToString();
                        }



                        //  }
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }
                //KTCN
                //if (Framework.Environment.Portal.rewrite == "1")
                //{
                //    row["url"] = "/Xem/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                //}
                //else
                //{
                //    row["url"] = "/Default.aspx?page=View&SubId=" + row["id"].ToString();
                //}
            }
            return dtEbook;
        }
        public DataTable SearchMagazineDetail(long MagazineId, int ItemPerPage, int CurrentPage)
        {
            
                    DataTable dt = objDEbook.SearchMagazineDetail(MagazineId, ItemPerPage, CurrentPage);
            if (Framework.Database.Table.CheckData(dt))
            {
                foreach (DataRow row in dt.Rows)
                {
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }

                    if (string.IsNullOrEmpty(row["images"].ToString()))
                    {

                        row["images"] = "/images/application.png";

                        
                    }
                    else
                    {
                        //if (Framework.Environment.File.Exists(Framework.Web.File.getPhysiPath(row["images"].ToString())))
                        //{
                            if (row["images"].ToString().Substring(0, 1) != "/")
                            {
                                row["images"] = "/" + row["images"].ToString();
                            }
                            else
                            {
                                row["images"] = row["images"].ToString();
                            }
                        //}
                        //else
                        //{
                        //    row["images"] = "/images/application.png";
                        //}    



                       
                    }
                }
            }
                    return dt;
        }
        public long GetToTalRecordSearchSearchMagazineDetail(long MagazineId)
        {

            return objDEbook.GetToTalRecordSearchSearchMagazineDetail(MagazineId);
        }


        public DataTable SearchMagazineByYear(string Year, long CollectionId, int ItemPerPage, int CurrentPage)
        {

            DataTable dt = objDEbook.SearchMagazineByYear(Year, CollectionId, ItemPerPage, CurrentPage);
            if (Framework.Database.Table.CheckData(dt))
            {
                foreach (DataRow row in dt.Rows)
                {
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet-tap-chi/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=MagazineDetail&SubId=" + row["id"].ToString();
                    }

                    if (string.IsNullOrEmpty(row["images"].ToString()))
                    {

                        row["images"] = "/images/application.png";


                    }
                    else
                    {

                        if (row["images"].ToString().Substring(0, 1) != "/")
                        {
                            row["images"] = "/" + row["images"].ToString();
                        }

                        if (Framework.Environment.File.Exists(Framework.Web.File.GetPhysipicPath(row["images"].ToString())))
                        {

                        }
                        else
                        {
                            row["images"] = "/images/application.png";
                        }    


                    }
                }
            }
            return dt;
        }
        public long GetToTalRecordSearchSearchMagazineByYear(string Year, long CollectionId)
        {

            return objDEbook.GetToTalRecordSearchMagazineByYear(Year,CollectionId);
        }

        public DataTable SearchEbookHeritage(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId = 0, long monHocId = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", long ItemPerPage = 0, long CurrentPage = 0, string TenTapChi = "", string SoTapChi = "", long DicTypeId = 0,string ItemList="")
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);
            DataTable dtEbook = objDEbook.SearchEbookHeritage(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, publishDateFrom, publishDateTo, Id, nganhHocId, monHocId, Top, OrderBy, PortalId, Language, ItemPerPage, CurrentPage, TenTapChi, SoTapChi, DicTypeId, ItemList);
            if (Framework.Database.Table.CheckData(dtEbook))
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    if (string.IsNullOrEmpty(row["images"].ToString()))
                    {

                        row["images"] = "/images/application.png";

                       
                    }
                    else
                    {
                       
                        if (row["images"].ToString().Substring(0, 1) != "/")
                        {
                            row["images"] = "/" + row["images"].ToString();
                        }



                        //  }
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }

            }
            return dtEbook;
        }
        public DataTable ThongKeTacGia(string where)
        {
            
            DataTable dtEbook = objDEbook.ThongKeTacGia(where);
            
            return dtEbook;
        }
        public DataTable SearchEbookValuate(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, string publishDateFrom = "", string publishDateTo = "", long Id = 0,long nganhHocId=0, long monHocId=0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", long ItemPerPage = 0, long CurrentPage = 0, string TenTapChi = "", string SoTapChi = "",long DicTypeId=0)
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);
            DataTable dtEbook = objDEbook.SearchEbookValuate(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, publishDateFrom, publishDateTo, Id, nganhHocId,monHocId, Top, OrderBy, PortalId, Language, ItemPerPage, CurrentPage, TenTapChi, SoTapChi, DicTypeId);
            Bussiness.Ebook.EbookFile objBEBookFile = new EbookFile();
            if (Framework.Database.Table.CheckData(dtEbook))
            {
                Framework.Database.Table.AddColumn(new Framework.Database.TableCls[] { new Framework.Database.TableCls("CountFile", "string")}, dtEbook);
                foreach (DataRow row in dtEbook.Rows)
                {
                    row["CountFile"] = objBEBookFile.CountEbookFile(Convert.ToInt64(row["Id"]));
                    if (string.IsNullOrEmpty(row["images"].ToString()))
                    {
                        
                            row["images"] = "/images/application.png";
                        
                        //if (row["TypeId"].ToString() == "9")
                        //{
                        //    row["images"] = "/images/image-icon.png";
                        //}
                        //if (row["TypeId"].ToString() == "30")
                        //{
                        //    row["images"] = "/images/magize.png";
                        //}
                    }
                    else
                    {
                        //if (!Framework.Environment.File.Exists(Framework.Web.File.GetPhysipicPath(row["images"].ToString())))
                        //{
                        //    row["images"] = "/images/EBook.png";
                        //}
                        //else
                        //{
                        if (row["images"].ToString().Substring(0, 1) != "/")
                        {
                            row["images"] = "/" + row["images"].ToString();
                        }



                        //  }
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }
               
            }
            return dtEbook;
        }
        public DataTable SearchEbookT36(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId = 0, long monHocId = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", long ItemPerPage = 0, long CurrentPage = 0, string TenTapChi = "", string SoTapChi = "", long DicTypeId = 0,string DoMat="")
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);
            DataTable dtEbook = objDEbook.SearchEbookT36(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, publishDateFrom, publishDateTo, Id, nganhHocId, monHocId, Top, OrderBy, PortalId, Language, ItemPerPage, CurrentPage, TenTapChi, SoTapChi, DicTypeId,DoMat);
            Bussiness.Ebook.EbookFile objBEBookFile = new EbookFile();
            if (Framework.Database.Table.CheckData(dtEbook))
            {
                Framework.Database.Table.AddColumn(new Framework.Database.TableCls[] { new Framework.Database.TableCls("CountFile", "string") }, dtEbook);
                foreach (DataRow row in dtEbook.Rows)
                {
                    row["CountFile"] = objBEBookFile.CountEbookFile(Convert.ToInt64(row["Id"]));
                    if (string.IsNullOrEmpty(row["images"].ToString()))
                    {

                        row["images"] = "/images/application.png";

                        //if (row["TypeId"].ToString() == "9")
                        //{
                        //    row["images"] = "/images/image-icon.png";
                        //}
                        //if (row["TypeId"].ToString() == "30")
                        //{
                        //    row["images"] = "/images/magize.png";
                        //}
                    }
                    else
                    {
                        //if (!Framework.Environment.File.Exists(Framework.Web.File.GetPhysipicPath(row["images"].ToString())))
                        //{
                        //    row["images"] = "/images/EBook.png";
                        //}
                        //else
                        //{
                        if (row["images"].ToString().Substring(0, 1) != "/")
                        {
                            row["images"] = "/" + row["images"].ToString();
                        }



                        //  }
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }

            }
            return dtEbook;
        }

        public DataTable SearchEbookOAI( string SubmitedFrom, string SubmitedTo,  long CollectionId, int status, long ItemPerPage = 0, long CurrentPage = 0)
        {
            
            DataTable dtEbook = objDEbook.SearchEbookOAI( SubmitedFrom, SubmitedTo,  CollectionId, status,  ItemPerPage, CurrentPage);
           

            
            return dtEbook;
        }
        public DataTable SearchEbooklrc(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", int ItemPerPage = 0, int CurrentPage = 0, string TenTapChi = "", string SoTapChi = "",long ProjectId=0)
        {
            
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);
            DataTable dtEbook = objDEbook.SearchEbooklrc(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, Id, Top, OrderBy, PortalId, Language, ItemPerPage, CurrentPage, TenTapChi, SoTapChi,ProjectId);
            if (Framework.Database.Table.CheckData(dtEbook))
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    if (row["images"].ToString() == "")
                    {
                        row["images"] = "/images/EBook.png";
                    }
                    else
                    {
                      
                        if (row["images"].ToString().Substring(0, 1) != "/")
                        {
                            row["images"] = "/" + row["images"].ToString();
                        }



                      
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }
               
            }
            return dtEbook;
        }
        public DataTable SearchArticle(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long Id = 0, string AuthorId = "", string OrgId = "", string Top = "", string OrderBy = "", string PortalId = "", string Language = "")
        {
            DataTable dtEbook = null;

            //if (OrgId > 0)
            //{
            //    Bussiness.Article.DicAuthor objBAuthor = new Article.DicAuthor();
            //    DataTable dtAuthor = objBAuthor.SearchDicAuthor("", "", OrgId, 0, PortalId, Language);

            //    dtEbook = objDEbook.SearchArticleStruc();
            //    foreach (DataRow row in dtAuthor.Rows)
            //    {
            //        string strAuthorId = row["id"].ToString();

            //        DataTable dtTemp = objDEbook.SearchArticle(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, Id, strAuthorId, OrgId, Top, OrderBy, PortalId, Language);
            //        if (dtTemp != null)
            //        {
            //            foreach (DataRow row1 in dtTemp.Rows)
            //            {
            //                if (dtEbook.Select("Id =" + row1["id"]).Length > 0)
            //                {

            //                }
            //                else
            //                {
            //                    dtEbook.Rows.Add(row1.ItemArray);
            //                }
            //            }
            //        }
            //    }
            //}
            //else
            //{
            dtEbook = objDEbook.SearchArticle(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, Id, AuthorId, OrgId, Top, OrderBy, PortalId, Language);
            // }
            if (dtEbook != null)
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    if (row["images"].ToString() == "")
                    {
                        row["images"] = "/images/no-image.jpg";
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }
            }
            return dtEbook;
        }
        public DataTable SearchArticle(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, int ItemPerPage, int CurrentPage, long Id = 0, string AuthorId = "", string OrgId = "", string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string ArticleName = "")
        {
            DataTable dtEbook = null;

            //if (OrgId > 0)
            //{
            //    Bussiness.Article.DicAuthor objBAuthor = new Article.DicAuthor();
            //    DataTable dtAuthor = objBAuthor.SearchDicAuthor("", "", OrgId, 0, PortalId, Language);

            //    dtEbook = objDEbook.SearchArticleStruc();
            //    foreach (DataRow row in dtAuthor.Rows)
            //    {
            //        string strAuthorId = row["id"].ToString();

            //        DataTable dtTemp = objDEbook.SearchArticle(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, ItemPerPage, CurrentPage, Id, strAuthorId, OrgId, Top, OrderBy, PortalId, Language);
            //        if (dtTemp != null)
            //        {
            //            foreach (DataRow row1 in dtTemp.Rows)
            //            {
            //                if (dtEbook.Select("Id =" + row1["id"]).Length > 0)
            //                {

            //                }
            //                else
            //                {
            //                    dtEbook.Rows.Add(row1.ItemArray);
            //                }
            //            }
            //        }
            //    }
            //}
            //else
            //{
            if (!string.IsNullOrEmpty(Author))
            {
                Author = FirstName(Author) + "," + LastName(Author);
            }
            dtEbook = objDEbook.SearchArticle(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, ItemPerPage, CurrentPage, Id, AuthorId, OrgId, Top, OrderBy, PortalId, Language, ArticleName);
            //   }
            if (dtEbook != null)
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    if (row["images"].ToString() == "")
                    {
                        row["images"] = "/images/PBook.png";
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }
            }
            return dtEbook;
        }
        public DataTable SearchArticleStruc()
        {
            return objDEbook.SearchArticleStruc();
        }
        public string BuildSQLSearchEbookBasic(string Keyword, string Field, string SearchType, string SearchIn, string TypeSQL = "")
        {
            string strSQL = "";
            if (TypeSQL == "Count")
            {
                strSQL = " Select count(Ebook.ItemXml.Id) From Ebook.ItemXml left outer join Ebook.item on Ebook.item.id =Ebook.itemXml.id   where 1=1 ";
            }
            else
            {
                strSQL = strSQL + " Select  ROW_NUMBER() OVER (ORDER BY submited desc ) AS [Index] ,Ebook.itemXml.[id],[Title] ,[Author] ,[Publisher]     ,[PublishDate] ";
                strSQL = strSQL + " ,[Keyword]      ,[xml]      ,[otherTitle]      ,[submited]      ,[createdBy]      ,[collectionId]  ,[images]      ,[totalView]      ,[totalDownload]      ,Ebook.item.[status]      ,[subjectId] ";
                strSQL = strSQL + " ,[lastUpdate]      ,[TypeId]      ,[UpdateBy]      ,Ebook.item.[AllowDownload], '''' as url, '''' as CreatedDate from Ebook.itemXml 	left outer join Ebook.item on Ebook.item.id =Ebook.itemXml.id 	left outer join Ebook.Collection on Ebook.item.collectionId=Ebook.Collection.Id 	  where 1=1 ";

            }
            switch (Field.ToUpper())
            {
                case "TITLE":
                    {
                        if (SearchType == "1")
                        {
                            strSQL = strSQL + "AND ( (title LIKE N'%" + Keyword + "%')";

                            strSQL = strSQL + "  or  ( Ebook.itemXml.[id] in (select itemId from Ebook.MetaDataValue where ( MetaDataFieldId=64 and value_Unsign like '%" + Framework.Web.Url.RemoveUnicode(Keyword) + "%')))) ";
                        }
                        else
                        {
                            strSQL = strSQL + "AND ( (title LIKE N'" + Keyword + "%')";

                            strSQL = strSQL + "  or  ( Ebook.itemXml.[id] in (select itemId from Ebook.MetaDataValue where ( MetaDataFieldId=64 and value_Unsign like '" + Framework.Web.Url.RemoveUnicode(Keyword) + "%'))))";
                        }
                        break;
                    }

                case "AUTHOR":
                    {
                        if (SearchType == "1")
                        {
                            strSQL = strSQL + "AND ( (Oldauthor LIKE N'%" + Keyword + "%')";

                            strSQL = strSQL + "   or ( Ebook.itemXml.[id] in (select itemId from Ebook.MetaDataValue where MetaDataFieldId=3 and substring(value_Unsign,CHARINDEX(',', value_Unsign)+1,100) + ' ' + substring(value_Unsign,0,CHARINDEX(',', value_Unsign))  Like '%" + Framework.Web.Url.RejectMarks(Keyword) + "%' ))) ";


                        }
                        else
                        {
                            strSQL = strSQL + "AND ( (title LIKE N'" + Keyword + "%')";

                            strSQL = strSQL + "  or  ( Ebook.itemXml.[id] in (select itemId from Ebook.MetaDataValue where ( MetaDataFieldId=64 and value_Unsign like '" + Framework.Web.Url.RemoveUnicode(Keyword) + "%'))))";
                        }
                        break;
                    }
                case "KEYWORD":
                    {
                        if (SearchType == "1")
                        {
                            strSQL = strSQL + "AND ( (keyword LIKE N'%" + Keyword + "%')";
                            strSQL = strSQL + "  or  ( Ebook.itemXml.[id] in (select itemId from Ebook.MetaDataValue where ( MetaDataFieldId=57 and value_Unsign like '%" + Framework.Web.Url.RemoveUnicode(Keyword) + "%'))))";
                        }
                        else
                        {
                            strSQL = strSQL + "AND ( (keyword LIKE N'" + Keyword + "%')";

                            strSQL = strSQL + "  or  ( Ebook.itemXml.[id] in (select itemId from Ebook.MetaDataValue where ( MetaDataFieldId=57 and value_Unsign like '" + Framework.Web.Url.RemoveUnicode(Keyword) + "%'))))";
                        }
                        break;
                    }
                case "PUBLISHER":
                    {
                        if (SearchType == "1")
                        {
                            strSQL = strSQL + "AND ( (publisher LIKE N'%" + Keyword + "%')";

                            strSQL = strSQL + "  or  ( Ebook.itemXml.[id] in (select itemId from Ebook.MetaDataValue where ( MetaDataFieldId=39 and value_Unsign like '%" + Framework.Web.Url.RemoveUnicode(Keyword) + "%')))) ";
                        }
                        else
                        {
                            strSQL = strSQL + "AND ( (publisher LIKE N'" + Keyword + "%')";

                            strSQL = strSQL + "  or  ( Ebook.itemXml.[id] in (select itemId from Ebook.MetaDataValue where ( MetaDataFieldId=39 and value_Unsign like '" + Framework.Web.Url.RemoveUnicode(Keyword) + "%'))))";
                        }
                        break;
                    }
                //default:
                //    {
                //        if (SearchType == "1")
                //        {
                //            strSQL = strSQL + "AND ( ( ( (title LIKE N'%" + Keyword + "%')";

                //            strSQL = strSQL + "  or  ( Ebook.itemXml.[id] in (select itemId from Ebook.MetaDataValue where ( MetaDataFieldId=64 and value_Unsign like '%" + Framework.Web.Url.RemoveUnicode(Keyword) + "%')))) ";

                //            strSQL = strSQL + "Or ( (keyword LIKE N'%" + Keyword + "%')";

                //            strSQL = strSQL + "  or  ( Ebook.itemXml.[id] in (select itemId from Ebook.MetaDataValue where ( MetaDataFieldId=57 and value_Unsign like '%" + Framework.Web.Url.RemoveUnicode(Keyword) + "%'))))  ";

                //            strSQL = strSQL + " ))";
                //        }
                //        break;
                //    }

            }
            return strSQL;
        }
        public string BuildSQLSearchPbookBasic(string Keyword, string Field, string SearchType, string SearchIn, string TypeSQL = "")
        {
            string strSQL = "";
            if (TypeSQL == "Count")
            {
                strSQL = " Select count(PrintBook.Bib.Bibid) From PrintBook.Bib   where 1=1 ";
            }
            else
            {
                strSQL = "Select  ROW_NUMBER() OVER (ORDER BY PrintBook.bib.CreatedTime desc ) AS [Index],  PrintBook.bib.bibid,PrintBook.GetISBD(bib.bibid) as ISBD, bib.bib_type_id, '''' as url,PrintBook.GetTitle(bib.bibid) as Title, url as url1, PrintBook.GetAuthor(bib.bibid) as Author, PrintBook.GetPublisher(bib.bibid) as Publisher, PrintBook.GetPublishDate(bib.bibid) as PublishDate,PrintBook.GetKeywordISBD(bib.bibid) as Keyword,  images, CreatedTime as submited,CreatedTime   from PrintBook.Bib left outer join PrintBook.bibxml on PrintBook.bibxml.bibid=PrintBook.bib.bibid  Where 1=1 ";
            }

            switch (Field.ToUpper())
            {
                case "TITLE":
                    {
                        if (SearchType == "1")
                        {
                            strSQL = strSQL + "and ( PrintBook.Bib.Bibid in (Select distinct(Bibid) from PrintBook.Bibdata where ( (Field='245' and subfield='a' and Data Like N'%" + Keyword + "%') or (Field='245' and subfield='a' and DataUnsign Like N'%" + Framework.Web.Url.RemoveUnicode(Keyword) + "%')  ) ) )";


                        }
                        else
                        {
                            strSQL = strSQL + "and (PrintBook.Bib.Bibid in (Select distinct(Bibid) from PrintBook.Bibdata where ( (Field='245' and subfield='a' and Data Like N'" + Keyword + "%') or (Field='245' and subfield='a' and DataUnsign Like N'" + Framework.Web.Url.RemoveUnicode(Keyword) + "%')  ) ) )";
                        }
                        break;
                    }

                case "AUTHOR":
                    {
                        if (SearchType == "1")
                        {
                            strSQL = strSQL + " AND PrintBook.Bib.BibId in (Select distinct(bibdata.BibId) from PrintBook.Bibdata where  ( ((Field='100' and subfield='a') or (Field='110' and subfield='a') or (Field='700' and subfield='a')) and (Data Like N'%" + Keyword + "%')) or ( ((Field='100' and subfield='a') or (Field='110' and subfield='a') or (Field='700' and subfield='a')) and (DataUnsign Like N'%" + Framework.Web.Url.RemoveUnicode(Keyword) + "%'))) ";

                        }
                        else
                        {
                            strSQL = strSQL + " AND PrintBook.Bib.BibId in (Select distinct(bibdata.BibId) from PrintBook.Bibdata where  ( ((Field='100' and subfield='a') or (Field='110' and subfield='a') or (Field='700' and subfield='a')) and (Data Like N'" + Keyword + "%')) or ( ((Field='100' and subfield='a') or (Field='110' and subfield='a') or (Field='700' and subfield='a')) and (DataUnsign Like N'" + Framework.Web.Url.RemoveUnicode(Keyword) + "%'))) ";


                        }
                        break;
                    }
                case "KEYWORD":
                    {
                        if (SearchType == "1")
                        {
                            strSQL = strSQL + "and ( PrintBook.Bib.Bibid in (Select distinct(Bibid) from PrintBook.Bibdata where ( (Field='653' and subfield='a' and Data Like N'%" + Keyword + "%') or (Field='653' and subfield='a' and DataUnsign Like N'%" + Framework.Web.Url.RemoveUnicode(Keyword) + "%')  ) ) )";
                        }
                        else
                        {
                            strSQL = strSQL + "and (PrintBook.Bib.Bibid in (Select distinct(Bibid) from PrintBook.Bibdata where ( (Field='653' and subfield='a' and Data Like N'" + Keyword + "%') or (Field='653' and subfield='a' and DataUnsign Like N'" + Framework.Web.Url.RemoveUnicode(Keyword) + "%')  ) ) )";
                        }
                        break;
                    }
                case "PUBLISHER":
                    {
                        if (SearchType == "1")
                        {
                            strSQL = strSQL + "and ( PrintBook.Bib.Bibid in (Select distinct(Bibid) from PrintBook.Bibdata where ( (Field='260' and subfield='c' and Data Like N'%" + Keyword + "%') or (Field='260' and subfield='c' and DataUnsign Like N'%" + Framework.Web.Url.RemoveUnicode(Keyword) + "%')  ) ) )";
                        }
                        else
                        {
                            strSQL = strSQL + "and (PrintBook.Bib.Bibid in (Select distinct(Bibid) from PrintBook.Bibdata where ( (Field='260' and subfield='c' and Data Like N'" + Keyword + "%') or (Field='260' and subfield='c' and DataUnsign Like N'" + Framework.Web.Url.RemoveUnicode(Keyword) + "%')  ) ) )";
                        }
                        break;
                    }


            }
            return strSQL;
        }

        public DataTable getOtherBookByID(String bookID)
        {
            Framework.Database.SqlDatabaseHelper objSqlHelper = new Framework.Database.SqlDatabaseHelper();
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("bookID",bookID,"in")
                
                
             };
            DataTable dtNews = null;
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        dtNews = objSqlHelper.ExecuteDataTable("Ebook.GetOtherEBooks", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }
            return dtNews;
        }

        public DataTable SearchBookBasic(string Keyword, string Field, string SearchType, string SearchIn, int CurrentPage, int ItemPerPage)
        {
            string strSQL = "";
            
            DataTable dtEBook = null;
            DataTable dtPBook = null;

            Framework.Database.TableCls[] Fields = new Framework.Database.TableCls[] {
             new Framework.Database.TableCls("Id","long"),
               new Framework.Database.TableCls("Title","string"),
               new Framework.Database.TableCls("Author","string"),
               new Framework.Database.TableCls("Publisher","string"),
               new Framework.Database.TableCls("PublishDate","string"),
               new Framework.Database.TableCls("ISBD","string"),
               new Framework.Database.TableCls("Keyword","string"),
               new Framework.Database.TableCls("submited","string"),
               new Framework.Database.TableCls("url","string"),
               new Framework.Database.TableCls("type","string"),
               new Framework.Database.TableCls("images","string"),
               
            };
            DataTable dtResult = Framework.Database.Table.CreateTable(Fields);
            if (SearchIn == "2")
            {

                //Tim kiem tai lieu so
                strSQL = BuildSQLSearchEbookBasic(Keyword, Field, SearchType, SearchIn);
                strSQL = "Begin  with s as ( " + strSQL;
                strSQL = strSQL + " ) select * from s where  ([Index] Between  Convert(varchar," + ((CurrentPage - 1) * ItemPerPage + 1).ToString() + ") AND  CONVERT(varchar, " + (CurrentPage * ItemPerPage).ToString() + ")  )  order by submited desc end";

                dtEBook = objDEbook.SearchBookBasic(strSQL);

            }
            else
            {
                if (SearchIn == "1")
                {

                    //tim kiem tai lieu in

                    strSQL = BuildSQLSearchPbookBasic(Keyword, Field, SearchType, SearchIn);

                    strSQL = "Begin  with s as ( " + strSQL;
                    strSQL = strSQL + " ) select * from s where  ([Index] Between  Convert(varchar," + ((CurrentPage - 1) * ItemPerPage + 1).ToString() + ") AND  CONVERT(varchar, " + (CurrentPage * ItemPerPage).ToString() + ")  )  order by createdTime desc end";


                    dtPBook = objDEbook.SearchBookBasic(strSQL);

                }
                else
                {
                    if (SearchIn == "0")
                    {
                        //Tim tat ca
                        strSQL = BuildSQLSearchEbookBasic(Keyword, Field, SearchType, SearchIn);
                        strSQL = "Begin  with s as ( " + strSQL;
                        strSQL = strSQL + " ) select * from s where  ([Index] Between  Convert(varchar," + ((CurrentPage - 1) * ItemPerPage + 1).ToString() + ") AND  CONVERT(varchar, " + (CurrentPage * ItemPerPage).ToString() + ")  )  order by submited desc end";

                        dtEBook = objDEbook.SearchBookBasic(strSQL);
                        Framework.Environment.LogFile.WriteLog(strSQL);
                        strSQL = BuildSQLSearchPbookBasic(Keyword, Field, SearchType, SearchIn);
                        strSQL = "Begin  with s as ( " + strSQL;
                        strSQL = strSQL + " ) select * from s where  ([Index] Between  Convert(varchar," + ((CurrentPage - 1) * ItemPerPage + 1).ToString() + ") AND  CONVERT(varchar, " + (CurrentPage * ItemPerPage).ToString() + ")  )  order by createdTime desc end";


                        dtPBook = objDEbook.SearchBookBasic(strSQL);
                      
                    }
                }
            }

            if (dtEBook != null)
            {
                foreach (DataRow row in dtEBook.Rows)
                {
                    DataRow row1 = dtResult.NewRow();
                    row1["id"] = row["id"].ToString();
                    row1["title"] = row["title"].ToString();
                    row1["author"] = row["author"].ToString();
                    row1["publisher"] = row["publisher"].ToString();
                    row1["publishDate"] = row["publishDate"].ToString();
                    row1["keyword"] = row["keyword"].ToString();
                    row1["submited"] = row["submited"].ToString();
                    row1["images"] = row["images"].ToString();
                    row1["type"] = "EBook";
                    row1["url"] = row["url"];
                    dtResult.Rows.Add(row1);
                }
            }
            if (Framework.Database.Table.CheckData(dtPBook))
            {
                foreach (DataRow row in dtPBook.Rows)
                {
                    DataRow row1 = dtResult.NewRow();
                    row1["title"] = row["title"].ToString();
                    row1["id"] = row["bibid"].ToString();
                    row1["author"] = row["author"].ToString();
                    row1["publisher"] = row["publisher"].ToString();
                    row1["publishDate"] = row["publishDate"].ToString();
                    row1["keyword"] = row["keyword"].ToString();
                    row1["submited"] = row["submited"].ToString();
                    row1["images"] = row["images"].ToString();
                    row1["type"] = "PBook";
                    row1["url"] = row["url"];
                    dtResult.Rows.Add(row1);
                }
            }
            DataView dv = new DataView(dtResult);

            

            dv.Sort = "publishDate Desc ";

            return dv.Table;
        }
        public long GetToTalRecordSearchEBookBasic(string Keyword, string Field, string SearchType, string SearchIn)
        {
            string strSQL = "";
            long TotalRecord = 0;
            long TotalEBookRecord = 0;
            long TotalPBookRecord = 0;
            if (SearchIn == "2")
            {

                //Tim kiem tai lieu so
                strSQL = BuildSQLSearchEbookBasic(Keyword, Field, SearchType, SearchIn, "Count");
                TotalEBookRecord = objDEbook.GetToTalRecordSearchEBookBasic(strSQL);

            }
            else
            {
                if (SearchIn == "1")
                {

                    //tim kiem tai lieu in

                    strSQL = BuildSQLSearchPbookBasic(Keyword, Field, SearchType, SearchIn, "Count");
                    TotalEBookRecord = objDEbook.GetToTalRecordSearchEBookBasic(strSQL);

                }
                else
                {
                    if (SearchIn == "0")
                    {
                        //Tim tat ca
                        strSQL = BuildSQLSearchEbookBasic(Keyword, Field, SearchType, SearchIn, "Count");
                        TotalEBookRecord = objDEbook.GetToTalRecordSearchEBookBasic(strSQL);

                        strSQL = BuildSQLSearchPbookBasic(Keyword, Field, SearchType, SearchIn, "Count");
                        TotalPBookRecord = objDEbook.GetToTalRecordSearchEBookBasic(strSQL);

                    }
                }
            }
            TotalRecord = TotalEBookRecord + TotalPBookRecord;
            return TotalRecord;

        }
        public long GetToTalRecordEBook(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, string publishDateFrom="", string publishDateTo="", long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "",string TenTapChi="",string SoTapChi="")
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            return objDEbook.GetToTalRecord(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId,  publishDateFrom,  publishDateTo, Id, Top, OrderBy, PortalId, Language,TenTapChi,SoTapChi);
        }
        public long GetToTalRecordEBookValuate(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId=0, long monHocId=0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "", long DicTypeId=0)
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            return objDEbook.GetToTalRecordValuate(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, publishDateFrom, publishDateTo, Id,  nganhHocId,monHocId, Top, OrderBy, PortalId, Language, TenTapChi, SoTapChi, DicTypeId);
        }
        public long GetToTalRecordEBookT36(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId = 0, long monHocId = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "", long DicTypeId = 0,string DoMat="")
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            return objDEbook.GetToTalRecordT36(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, publishDateFrom, publishDateTo, Id, nganhHocId, monHocId, Top, OrderBy, PortalId, Language, TenTapChi, SoTapChi, DicTypeId,DoMat);
        }
        public long GetToTalRecordEBookHeritage(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, string publishDateFrom = "", string publishDateTo = "", long Id = 0, long nganhHocId = 0, long monHocId = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "", long DicTypeId = 0,string ItemList="")
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            return objDEbook.GetToTalRecordHeritage(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, publishDateFrom, publishDateTo, Id, nganhHocId, monHocId, Top, OrderBy, PortalId, Language, TenTapChi, SoTapChi, DicTypeId,ItemList);
        }
        public long GetToTalRecordEBookOAI( string SubmitedFrom, string SubmitedTo,  long CollectionId, int status)
        {
            

            return objDEbook.GetToTalRecordEBookOAI( SubmitedFrom, SubmitedTo,  CollectionId, status);
        }
        public long GetToTalRecordEBooklrc(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "",long ProjectId=0)
        {
            
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            return objDEbook.GetToTalRecordlrc(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, Id, Top, OrderBy, PortalId, Language, TenTapChi, SoTapChi,ProjectId);
        }
        public long GetToTalRecordEBookTapChiChanNuoi(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "", long MagazineId = 0, long SoTapChiId = 0)
        {
            if (!string.IsNullOrEmpty(Author))
            {
                Author = FirstName(Author) + "," + LastName(Author);
            }
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            return objDEbook.GetToTalRecordTapChiChanNuoi(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, Id, Top, OrderBy, PortalId, Language, TenTapChi, SoTapChi, MagazineId, SoTapChiId);
        }
        public long GetToTalRecordEBookBaoND(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "", long MagazineId = 0, long SoTapChiId = 0, string NgayPhatHanhFrom="",string NgayPhatHanhTo="")
        {
            
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            return objDEbook.GetToTalRecordBaoND(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, Id, Top, OrderBy, PortalId, Language, TenTapChi, SoTapChi, MagazineId, SoTapChiId,NgayPhatHanhFrom,NgayPhatHanhTo);
        }
        public long GetToTalRecordEBookHVCT1(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, long Id = 0, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "")
        {
            if (!string.IsNullOrEmpty(Author))
            {
                Author = FirstName(Author) + "," + LastName(Author);
            }
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            return objDEbook.GetToTalRecord(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId,"","", Id, Top, OrderBy, PortalId, Language, TenTapChi, SoTapChi);
        }
        public long GetToTalRecordEBookDHY(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, long Id = 0, string Op1 = "", string Op2 = "", string Op3 = "", string Op4 = "", string Op5 = "", string Content = "")
        {
            DataTable dtBook = objDEbook.GetTotalRecordSearchDHY(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, Op1, Op2, Op3, Op4, Op5);
            //if (Content == "")
            //{

            //    return dtBook.Rows.Count;
            //}
            //else
            //{
            //    if (Title == "" && PublishDate == "" && Publisher == "" && Keyword == "")
            //    {
            //        for (int i = 0; i < dtBook.Rows.Count; i++)
            //        {
            //            dtBook.Rows.RemoveAt(i);
            //        }
            //    }

            //    Indexer.IntranetIndex objBIntranet = new Indexer.IntranetIndex(Framework.Environment.Setting.GetKey("Index"));

            //    DataTable dtId = objBIntranet.SearchId(Content, "Content", 1000);
            //    string Ids="";
            //    long lnIdsNotIn=0;
            //    if (dtId != null)
            //    {
            //        foreach (DataRow row in dtId.Rows)
            //        {
            //            if (dtBook.Select ("Id="+row["id"].ToString()).Length>0)
            //            {
            //            }
            //            else
            //            {
            //                DataRow row1 = dtBook.NewRow();
            //                row1["id"] = row["id"];
            //                dtBook.Rows.Add(row1);
            //            }
            //        }

            //    }

            return dtBook.Rows.Count;

            //  }
        }
        public long GetToTalRecordArticle(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long Id = 0, string AuthorId = "", string OrgId = "", string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string ArticleName = "")
        {

            if (AuthorId.Length > 0)
            {
                Author = "";

            }
            long lnTotalRecord = 0;
            //if (OrgId > 0)
            //{
            //    Bussiness.Article.DicAuthor objBAuthor = new Article.DicAuthor();
            //    DataTable dtAuthor = objBAuthor.SearchDicAuthor("", "", OrgId, 0, PortalId, Language);


            //    foreach (DataRow row in dtAuthor.Rows)
            //    {
            //        string strAuthorId = row["id"].ToString();

            //        DataTable dtTemp = objDEbook.SearchArticle(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId,  Id, strAuthorId, OrgId, Top, OrderBy, PortalId, Language);
            //        if (dtTemp != null)
            //        {
            //            foreach (DataRow row1 in dtTemp.Rows)
            //            {
            //                lnTotalRecord++;
            //            }
            //        }
            //    }
            //}
            //else
            //{
            lnTotalRecord = objDEbook.GetToTalRecordArticle(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, Id, AuthorId, OrgId, Top, OrderBy, PortalId, Language, ArticleName);

            // }
            return lnTotalRecord;
        }
        public DataTable SearchEbook(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, int itemPerPage, int currentPage, long Id, string Top = "", string OrderBy = "", string PortalId = "", string Language = "")
        {
            DataTable dtEbook = objDEbook.SearchEbook(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, itemPerPage, currentPage, Id, Top, OrderBy, PortalId, Language);
            if (Framework.Database.Table.CheckData(dtEbook))
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    row["CreatedDate"] = Convert.ToDateTime(row["Submited"]).ToString("dd/MM/yyyy hh:mm:ss");
                    if (row["images"].ToString() == "")
                    {
                        row["images"] = "/images/PBook.png";
                    }
                    else
                    {
                        if (Framework.Environment.File.Exists(Framework.Environment.File.GetPhysipicPath(row["images"].ToString())))
                        {
                            if (row["images"].ToString().Substring(0, 1) != "/")
                            {
                                row["images"] = "/" + row["images"].ToString();
                            }
                        }
                        else
                        {
                            row["images"] = "/images/PBook.png";
                        }
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }
            }
            return dtEbook;
        }
        public DataTable SearchEbookLRC(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, int itemPerPage, int currentPage, long Id, string Top = "", string OrderBy = "", string PortalId = "", string Language = "",long ProjectId=0)
        {
            DataTable dtEbook = objDEbook.SearchEbooklrc(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId,Id,"",OrderBy, PortalId, Language,itemPerPage, currentPage,"","",0);
            if (Framework.Database.Table.CheckData(dtEbook))
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    row["CreatedDate"] = Convert.ToDateTime(row["Submited"]).ToString("dd/MM/yyyy hh:mm:ss");
                    if (row["images"].ToString() == "")
                    {
                        row["images"] = "/images/PBook.png";
                    }
                    else
                    {
                        if (Framework.Environment.File.Exists(Framework.Environment.File.GetPhysipicPath(row["images"].ToString())))
                        {
                            if (row["images"].ToString().Substring(0, 1) != "/")
                            {
                                row["images"] = "/" + row["images"].ToString();
                            }
                        }
                        else
                        {
                            row["images"] = "/images/PBook.png";
                        }
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }
            }
            return dtEbook;
        }
        public DataTable SearchEbookDHKTCN(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, int itemPerPage, int currentPage, long Id, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string TenTapChi = "", string SoTapChi = "")
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);


            DataTable dtEbook = objDEbook.SearchEbook(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, itemPerPage, currentPage, Id, Top, OrderBy, PortalId, Language, TenTapChi, SoTapChi);
            if (dtEbook != null)
            {

                dtEbook = ProcessImageAndUrl(dtEbook);
            }
            return dtEbook;
        }
        public DataTable ProcessImageAndUrl(DataTable dt)
        {
            if (dt != null)
            {
                foreach (DataRow row in dt.Rows)
                {
                   

                    try
                    {
                        row["CreatedDate"] = Convert.ToDateTime(row["Submited"].ToString()).ToString("dd/MM/yyyy HH:mm:ss tt");
                    }
                    catch
                    {
                       // row["CreatedDate"] = row["Submited"].ToString();
                    }
                    if (row["images"].ToString() == "")
                    {
                        row["images"] = "/images/EBook.png";
                    }
                    else
                    {
                        //if (!Framework.Environment.File.Exists(Framework.Web.File.getPhysiPath(row["images"].ToString())))
                        //{
                        //    row["images"] = "/images/EBook.png";
                        //}
                        //else
                        //{
                            if (row["images"].ToString().Substring(0, 1) != "/")
                            {
                                row["images"] = "/" + row["images"].ToString();
                            }


                       // }
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }
            }
            return dt;
        }
        public DataTable SearchEbookHVCT1(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, int itemPerPage, int currentPage, long Id, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string ISBN = "", string Extract = "", string TenTapChi="" , string SoTapChi="" )
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            DataTable dtEbook = objDEbook.SearchEbook(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, itemPerPage, currentPage, Id, Top, OrderBy, PortalId, Language,TenTapChi,SoTapChi);
            if (dtEbook != null)
            {
                dtEbook = ProcessImageAndUrl(dtEbook);
            }
            return dtEbook;
        }
        public DataTable SearchEbookDHY(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, int itemPerPage, int currentPage, long Id, string Top = "", string OrderBy = "", string PortalId = "", string Language = "", string Op1 = "", string Op2 = "", string Op3 = "", string Op4 = "", string Op5 = "", string Content = "")
        {
            DataTable dtEbook = objDEbook.SearchEbookDHY(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, itemPerPage, currentPage, Id, Top, OrderBy, PortalId, Language);
            //if (Content == "")
            //{
            if (dtEbook != null)
            {
                dtEbook = ProcessImageAndUrl(dtEbook);
            }
            //}
            //else
            //{
            //    if (Title == "" && PublishDate == "" && Publisher == "" && Keyword == "")
            //    {
            //        for (int i = 0; i < dtEbook.Rows.Count; i++)
            //        {
            //            dtEbook.Rows.RemoveAt(i);
            //        }
            //    }
            //    Indexer.IntranetIndex objBIntranet = new Indexer.IntranetIndex(Framework.Environment.Setting.GetKey("Index"));

            //    DataTable dtId = objBIntranet.SearchId(Content, "Content", 1000);
            //    string Ids = "";
            //    long lnIdsNotIn = 0;
            //    if (dtId != null)
            //    {
            //        foreach (DataRow row in dtId.Rows)
            //        {
            //            if (dtEbook.Select("Id=" + row["id"].ToString()).Length > 0)
            //            {

            //            }
            //            else
            //            {
            //                DataTable temp=GetDataTableEbookById(Convert.ToInt64(row["id"].ToString()));
            //                DataRow row1 = dtEbook.NewRow();
            //                row1["Title"] = temp.Rows[0]["Title"].ToString();
            //                row1["Author"] = temp.Rows[0]["Author"].ToString();
            //                row1["Publisher"] = temp.Rows[0]["Publisher"].ToString();
            //                row1["PublishDate"] = temp.Rows[0]["PublishDate"].ToString();
            //                if (Framework.Environment.Portal.rewrite == "1")
            //                {
            //                    row1["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(temp.Rows[0]["Title"].ToString(), temp.Rows[0]["Id"].ToString());
            //                }
            //                else
            //                {
            //                    row1["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
            //                }
            //                if (temp.Rows[0]["images"].ToString() == "")
            //                {
            //                    row1["images"] = "/images/PBook.png";
            //                }
            //                else
            //                {
            //                    if (!Framework.Environment.File.Exists(Framework.Environment.Setting.GetKey("PathRoot") + "\\" + temp.Rows[0]["images"].ToString()))
            //                    {
            //                        row1["images"] = "/images/PBook.png";
            //                    }
            //                    else
            //                    {
            //                        if (temp.Rows[0]["images"].ToString().Substring(0, 1) != "/")
            //                        {
            //                            row1["images"] = "/" + temp.Rows[0]["images"].ToString();
            //                        }


            //                    }
            //                }
            //                row1["CreatedDate"] = Convert.ToDateTime(temp.Rows[0]["Submited"].ToString()).ToString("dd/MM/yyyy hh:mm:ss");
            //                dtEbook.Rows.Add(row1);
            //            }
            //        }

            //    }
            //}
            return dtEbook;
        }
        public string GetTotalRecordSearch(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId,string TenTapChi="",string SoTapChi="")
        {
            //if (!string.IsNullOrEmpty(Author))
            //{
            //    Author = FirstName(Author) + "," + LastName(Author);
            //}
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            DataTable dtEbook = objDEbook.GetTotalRecordSearch(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId,TenTapChi,SoTapChi);

            if (dtEbook.Rows.Count > 0)
            {
                return dtEbook.Rows[0][0].ToString();
            }
            else
            {
                return "0";
            }


        }
        public string GetTotalRecordSearchLRC(string Title, string Author, string Publisher, string PublishDate, string Keyword, string SubmitedFrom, string SubmitedTo, long UserId, long CollectionId, int status, long ToPicId, long SubjectId, string TenTapChi = "", string SoTapChi = "")
        {
            Author = Framework.Environment.HtmlFunction.RemoteWhitSpace(Author);
            Title = Framework.Environment.HtmlFunction.RemoteWhitSpace(Title);
            Publisher = Framework.Environment.HtmlFunction.RemoteWhitSpace(Publisher);
            Keyword = Framework.Environment.HtmlFunction.RemoteWhitSpace(Keyword);

            DataTable dtEbook = objDEbook.GetTotalRecordSearch(Title, Author, Publisher, PublishDate, Keyword, SubmitedFrom, SubmitedTo, UserId, CollectionId, status, ToPicId, SubjectId, TenTapChi, SoTapChi);

            if (dtEbook.Rows.Count > 0)
            {
                return dtEbook.Rows[0][0].ToString();
            }
            else
            {
                return "0";
            }


        }
        public DataTable ReportBienMucEbook(long UserId, string FromDate, string ToDate)
        {
            return objDEbook.ReportBienMucEbook(UserId, FromDate, ToDate);
        }
        public void SaveEbook(Entities.Ebook.Ebook objEbook, long UserId)
        {
           
            string Output = objDEbook.SaveEbook(objEbook, UserId);
            if (objEbook.Id == 0)
            {
                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog("Thêm mới tài liệu ID : " + Output.ToString(), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBook", "Add"));
            }
            else
            {
                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog("Chỉnh sửa tài liệu ID : " + Output.ToString(), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBook", "Edit"));
            }
        }
        public void DeleteEbook(long Id)
        {


            Bussiness.Ebook.EbookFile objEbookFile = new EbookFile();
            Bussiness.Ebook.EbookItem objBEbookItemCloud = new EbookItem(Framework.Environment.Portal._cloudElibConnectionString);
            DataTable dtFile = objEbookFile.SearchEbookFile("", Id);
            if (Framework.Database.Table.CheckData(dtFile))
            {
                foreach (DataRow row in dtFile.Rows)
                {
                    string url = row["url"].ToString();
                    Framework.Web.File.DeleteFile(Framework.Web.File.GetPhysipicPath("/" + url));
                }
            }

            DataTable temp = objDEbook.GetDataTableEbookById(Id);

            if (Framework.Database.Table.CheckData(temp))
            {
                objDEbook.DeleteEbook(Id);


                Bussiness.Common.Users.SaveLog(new Entities.Common.UserLog("Xóa tài liệu ID : " + Id.ToString() + "::" + temp.Rows[0]["Title"].ToString() + "::" + temp.Rows[0]["author"].ToString() + temp.Rows[0]["PublishDate"].ToString() + "::" + temp.Rows[0]["Publisher"].ToString(), DateTime.Now, new Bussiness.Common.Users().GetCurrrentUserID(), Framework.Web.Url.ClientIp(), Framework.Environment.Setting.GetKey("TypeApplication"), "EBook", "Delete"));
            }

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
                    long iItemCloudId = objBEbookItemCloud.GetEbookItemCloud(Id);
                    if (iItemCloudId > 0)
                    {
                        DataAccess.Ebook.Ebook objDEbookCloud = new DataAccess.Ebook.Ebook(Framework.Environment.Portal._cloudElibConnectionString);
                       
                       
                        objDEbookCloud.DeleteEbook(iItemCloudId);
                    }
                }
                else
                {
                    long iItemCloudId = objBEbookItemCloud.GetEbookItemCloud(Id);
                    if (iItemCloudId > 0)
                    {
                        objBEbookItemCloud.UpdateDeleteCloud(iItemCloudId);
                    }
                }
            }


           
           

        }
        public void SaveBookLog(string CardNumber, long ReaderId, long BookId, long Page, long Size, int Type, string Ip)
        {
            //Bussiness.Common.Reader objBReader = new Common.Reader();
            //DataTable dt=objBReader.GetDataTableReaderByCardNumber(CardNumber);
            //if (Framework.Database.Table.CheckData(dt))
            //{
            //    long iReaderId = 0;
            //    try
            //    {
            //        iReaderId = Convert.ToInt64(dt.Rows[0]["id"]);
            //    }
            //    catch
            //    {
            //    }
            //    if (iReaderId > 0)
            //    {
            //        ReaderId = iReaderId;
            //    }
            //    objDEbook.SaveBookLog(CardNumber, ReaderId, BookId, Page, Size, Type, Ip);
            //}
            objDEbook.SaveBookLog(CardNumber, ReaderId, BookId, Page, Size, Type, Ip);
        }
        public void SaveBookLogLRC(string CardNumber, long ReaderId, long BookId, long Page, long Size, int Type, string Ip)
        {
            objDEbook.SaveBookLogLRC(CardNumber, ReaderId, BookId, Page, Size, Type, Ip);
        }
        public DataTable GetTopDownloadBook(int count)
        {
            return objDEbook.GetTopDownloadBook(count);
        }

        public string GetUrlDownload(long EBookFileId, string PortalId, long ReaderId, string CardNumber, string Ip)
        {

            Bussiness.Ebook.EbookFile objBEbookFile = new EbookFile();

            //Truong hop link download chua ton tai
            DataTable dtEbookFile = objBEbookFile.GetDataTableEbookFileOldById(EBookFileId);


            if (dtEbookFile.Rows.Count > 0)
            {
                //  DataTable dtEbook = GetDataTableEbookById(Convert.ToInt64(dtEbookFile.Rows[0]["EbookId"].ToString()));
                string url = dtEbookFile.Rows[0]["url"].ToString();

                objDEbook.SaveUrlDownload(EBookFileId, url, PortalId, ReaderId, CardNumber, Ip);
                return url;
            }
            else
            {
                return "";
            }





            //Bussiness.Ebook.EbookFile objBEbookFile = new EbookFile();
            //string Url = objBEbookFile.CheckUrlDownloadLinkExist(EBookFileId);
            //if (Url == "")
            //{
            //    //Truong hop link download chua ton tai
            //    DataTable dtEbookFile = objBEbookFile.GetDataTableEbookFileOldById(EBookFileId);


            //    if (dtEbookFile.Rows.Count > 0)
            //    {
            //        //  DataTable dtEbook = GetDataTableEbookById(Convert.ToInt64(dtEbookFile.Rows[0]["EbookId"].ToString()));
            //        string strPath = "E:\\wwwrot\\TLDienTu\\" + dtEbookFile.Rows[0]["url"].ToString().Replace("/", "\\");
            //        //  string strPath = "H:\\BKTailieudientu\\" + dtEbookFile.Rows[0]["url"].ToString().Replace("/", "\\");
            //        string strDesFile = Framework.Environment.Setting.GetKey("PathRoot").ToString();


            //        Guid id = Guid.NewGuid();
            //        string strId = id.ToString();


            //        string DirPath = strDesFile + "\\Upload\\" + PortalId + "\\" + strId;
            //        DirectoryInfo d = new DirectoryInfo(DirPath);
            //        if (!d.Exists)
            //        {
            //            d.Create();
            //        }
            //        Framework.Environment.RSACSPCls Rsa = new Framework.Environment.RSACSPCls();

            //        //  string OutputFileName = FrameWork.Web.File.getFileNameDate() + dtEbookFile.Rows[0]["fileExt"].ToString();
            //        string OutputFileName = Framework.Web.File.getFileNameDate() + dtEbookFile.Rows[0]["fileformat"].ToString();
            //        strDesFile = strDesFile + "\\Upload\\" + PortalId + "\\" + strId + "\\" + OutputFileName;
            //        string url = "Upload/" + PortalId + "/" + strId + "/" + OutputFileName;
            //        Framework.Web.File.CopyTo(strPath, strDesFile);
            //        objDEbook.SaveUrlDownload(EBookFileId, url, PortalId, ReaderId, CardNumber, Ip);
            //        return url;
            //    }
            //    else
            //    {
            //        return "";
            //    }
            //}
            //else
            //{
            //    objDEbook.SaveUrlDownload(EBookFileId, Url, PortalId, ReaderId, CardNumber, Ip);

            //    return Url;
            //}



            //   Bussiness.Ebook.EbookFile objBEbookFile = new EbookFile();
            //string Url=   objBEbookFile.CheckUrlDownloadLinkExist(EBookFileId);
            //if (Url == "")
            //{
            //    //Truong hop link download chua ton tai
            //    DataTable dtEbookFile = objBEbookFile.GetDataTableEbookFileById(EBookFileId);


            //    if (dtEbookFile.Rows.Count > 0)
            //    {
            //        DataTable dtEbook = GetDataTableEbookById(Convert.ToInt64(dtEbookFile.Rows[0]["EbookId"].ToString()));
            //        string strPath = Framework.Environment.Setting.GetKey("PathRoot").ToString() + "\\" + dtEbookFile.Rows[0]["url"].ToString().Replace("/", "\\");
            //        string strDesFile = Framework.Environment.Setting.GetKey("PathRoot").ToString();


            //        Guid id = Guid.NewGuid();
            //        string strId = id.ToString();


            //        string DirPath = strDesFile + "\\Upload\\" + PortalId + "\\" + strId;
            //        DirectoryInfo d = new DirectoryInfo(DirPath);
            //        if (!d.Exists)
            //        {
            //            d.Create();
            //        }
            //        Framework.Environment.RSACSPCls Rsa = new Framework.Environment.RSACSPCls();

            //        string OutputFileName = FrameWork.Web.File.getFileNameDate() + dtEbookFile.Rows[0]["fileExt"].ToString();
            //        strDesFile = strDesFile + "\\Upload\\" + PortalId + "\\" + strId + "\\" + OutputFileName;
            //        string url = "Upload/" + PortalId + "/" + strId + "/" + OutputFileName;
            //        FrameWork.Web.File.CopyTo(strPath, strDesFile);
            //        objDEbook.SaveUrlDownload(EBookFileId, url, PortalId, ReaderId, CardNumber, Ip);
            //        return url;
            //    }
            //    else
            //    {
            //        return "";
            //    }
            //}
            //else

            //{
            //    objDEbook.SaveUrlDownload(EBookFileId, Url, PortalId, ReaderId, CardNumber, Ip);

            //    return Url;
            //}

        }
        public DataTable GetTopReadBook(int count)
        {
            return objDEbook.GetTopReadBook(count);
        }

        public DataTable SearchSimpleEbook(string Keyword)
        {
            DataTable dtEbook = objDEbook.SearchSimpleEbook(Keyword);

            if (dtEbook != null)
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    if (row["images"].ToString() == "")
                    {
                        row["images"] = "/images/PBook.png";
                    }
                    else
                    {
                        if (!Framework.Environment.File.Exists(Framework.Environment.Setting.GetKey("PathRoot") + "\\" + row["images"].ToString()))
                        {
                            row["images"] = "/images/PBook.png";
                        }
                        else
                        {
                            if (row["images"].ToString().Substring(0, 1) != "/")
                            {
                                row["images"] = "/" + row["images"].ToString();
                            }



                        }
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }
            }
            return dtEbook;

        }
        public DataTable SearchSimpleEbook(string Keyword, int ItemPerPage, int CurrentPage)
        {
            DataTable dtEbook = objDEbook.SearchSimpleEbook(Keyword, ItemPerPage, CurrentPage);
            if (Framework.Database.Table.CheckData(dtEbook))
            {
                foreach (DataRow row in dtEbook.Rows)
                {
                    if (row["images"].ToString() == "")
                    {
                        row["images"] = "/images/EBook.png";
                    }
                    else
                    {
                        if (!Framework.Environment.File.Exists(Framework.Environment.Setting.GetKey("PathRoot") + "\\" + row["images"].ToString()))
                        {
                            row["images"] = "/images/EBook.png";
                        }
                        else
                        {
                            if (row["images"].ToString().Substring(0, 1) != "/")
                            {
                                row["images"] = "/" + row["images"].ToString();
                            }


                        }
                    }
                    if (Framework.Environment.Portal.rewrite == "1")
                    {
                        row["url"] = "/Chi-tiet/" + Framework.Web.Url.GenerateURL(row["title"], row["id"]);
                    }
                    else
                    {
                        row["url"] = "/Default.aspx?page=EbookDetail&SubId=" + row["id"].ToString();
                    }
                }
            }
            return dtEbook;

        }
        public long GetTotalRecordSearchSimpleEbook(string Keyword)
        {
            return objDEbook.GetTotalRecordSearchSimpleEbook(Keyword);

        }

        public DataTable ListSavedBook(int p)
        {
            DataTable dtBBook = objDEbook.ListSavedBook(p);
            dtBBook = ProcessImageAndUrl(dtBBook);
            return dtBBook;
        }

        public DataTable ListNewEBookInCollection(string p1, int p2)
        {
            DataTable dtBBook = objDEbook.ListNewEBookInCollection(p1, p2);
            dtBBook = ProcessImageAndUrl(dtBBook);
            return dtBBook;
        }
        public DataTable ReportEbookViewAndDownload(string Title, string Author, string  Publisher, string PublishDate, string Keyword, string StartTime, string EndTime,int Top, int Type=0)
        {
           DataTable dt=objDEbook.ReportEbookViewAndDownload( Title,  Author,   Publisher,  PublishDate,  Keyword,  StartTime,  EndTime, Top);
           int i = 1;
            if (Framework.Database.Table.CheckData(dt))
            {
                 foreach (DataRow row in dt.Rows)
              {
                  row["TotalView"] = objDEbook.GetNumberEbookViewAndDownload(Convert.ToInt64(row["BookId"].ToString()), StartTime, EndTime, 1);
                  row["TotalDownload"] = objDEbook.GetNumberEbookViewAndDownload(Convert.ToInt64(row["BookId"].ToString()), StartTime, EndTime, 2);
                  row["Index"] = i;
              }
            }
            if (Type > 0)
            {
                if (Type == 1)
                {
                    DataView dv = dt.DefaultView;
                    dv.Sort = "TotalView Desc";
                    dt = dv.ToTable(true);
                }
                if (Type == 2)
                {
                    DataView dv = dt.DefaultView;
                    dv.Sort = "TotalDownload Desc";
                    dt = dv.ToTable(true);
                }
            }
            return dt;
        }
        public DataTable ReportEbookNotView( string StartTime, string EndTime,int Type)
        {
            DataTable dt = objDEbook.ReportEbookNotView( StartTime, EndTime,Type);
            
            return dt;
        }
        public string FirstName(string fullName)
        {
            int post = fullName.LastIndexOf(" ");
            string firstName = "";

            if (post > 0)
            {

                firstName = fullName.Substring(0, post);
            }
            return firstName;
        }

        public string LastName(string fullName)
        {
            int post = fullName.LastIndexOf(" ");

            string lastName = "";
            if (post > 0)
            {
                lastName = fullName.Substring(post);

            }
            else
            {
                lastName = fullName;
            }
            return lastName;
        }
        public long CountEbookOfTopic(long ebookId)
        {
            return objDEbook.CountEbookOfTopic(ebookId);
        }
        
    }
}
