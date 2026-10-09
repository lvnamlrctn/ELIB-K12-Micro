using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Web;
using System.Web.SessionState;

using System.Xml;

namespace ElibWeb.Admin.Hander.EBook
{
    /// <summary>
    /// Summary description for EBook
    /// </summary>
    public class EBook : IHttpHandler, IRequiresSessionState
    {

        Bussiness.Ebook.Ebook objBEBook = new Bussiness.Ebook.Ebook();
        Bussiness.Ebook.MetaDataFieldRegistery objBMetaDataFieldRegistery = new Bussiness.Ebook.MetaDataFieldRegistery();
        Bussiness.Ebook.MetaDataValue objBMetaDataValue = new Bussiness.Ebook.MetaDataValue();
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json";


            string scmd = "";
            try
            {
                scmd = context.Request.Form["cmd"];
            }
            catch
            {

            }
            string sOut = "";
            switch (scmd)
            {
                case "Search":
                    {
                        string search = context.Request.Form["search[value]"];
                        string draw = context.Request.Form["draw"];
                        string order = context.Request.Form["order[0][column]"];
                        string orderDir = context.Request.Form["order[0][dir]"];
                        int startRec = Convert.ToInt32(context.Request.Form["start"]);
                        int pageSize = Convert.ToInt32(context.Request.Form["length"]);
                        int skip = startRec != null ? Convert.ToInt32(startRec) : 0;
                        int indexPage = (skip / pageSize) + 1;
                        string sTitle = context.Request.Form["sTitle"];
                        string sAuthor = context.Request.Form["sAuthor"];
                        var sPublisher = context.Request.Form["sPublisher"];
                        var sSoTapChi = context.Request.Form["sSoTapChi"];
                        var sTenTapChi = context.Request.Form["sTenTapChi"];
                        var sFromYear = context.Request.Form["sFromYear"];
                        var sToYear = context.Request.Form["sToYear"];

                        string sUserId = context.Request.Form["sUserId"];
                        string sId = context.Request.Form["sId"];
                        string sCollectionId = context.Request.Form["sCollectionId"];
                        string sTopicId = context.Request.Form["sTopicId"];
                        string sSubjectId = context.Request.Form["sSubjectId"];
                        string sDigTypeId = context.Request.Form["sDigType"];
                        string sKeyword = context.Request.Form["sKeyword"]; ;
                        string sStatus = context.Request.Form["sStatus"];

                        string sStartTime = context.Request.Form["sStartTime"];
                        string sEndTime = context.Request.Form["sEndTime"];
                        var dStartTime = DateTime.MinValue;
                        try
                        {
                            if (!string.IsNullOrEmpty(sStartTime))
                            {
                                dStartTime = Convert.ToDateTime(sStartTime);
                            }
                        }
                        catch
                        {

                        }
                        var dEndTime = DateTime.MinValue;
                        try
                        {
                            if (!string.IsNullOrEmpty(sEndTime))
                            {
                                dEndTime = Convert.ToDateTime(sEndTime);
                            }
                        }
                        catch
                        {

                        }
                        long iUserId = 0;
                        try
                        {
                            iUserId = Convert.ToInt64(sUserId);
                        }
                        catch
                        {

                        }
                        long iCollectionId = 0;
                        try
                        {
                            iCollectionId = Convert.ToInt64(sCollectionId);
                        }
                        catch
                        {

                        }
                      

                        long iTopicId = 0;
                        try
                        {
                            iTopicId = Convert.ToInt64(sTopicId);
                        }
                        catch
                        {

                        }

                        long iSubjectId = 0;
                        try
                        {
                            iSubjectId = Convert.ToInt64(sSubjectId);
                        }
                        catch
                        {

                        }


                        long iId = 0;
                        try
                        {
                            iId = Convert.ToInt64(sId);
                        }
                        catch
                        {

                        }

                        long iDicTypeId = 0;
                        try
                        {
                            iDicTypeId = Convert.ToInt64(sDigTypeId);
                        }
                        catch
                        {

                        }
                        int iStatus = 0;
                        try
                        {
                            iStatus = Convert.ToInt16(iStatus);

                        }
                        catch
                        {

                        }


                        sOut = Search(sTitle, sAuthor, sPublisher, "", sKeyword, Framework.Environment.DateTimeFunction.ConvertToSqlDate( sStartTime), Framework.Environment.DateTimeFunction.ConvertToSqlDate(sEndTime), iUserId, iCollectionId, iStatus, iTopicId, iSubjectId, sSoTapChi, sTenTapChi, sFromYear, sToYear, indexPage, pageSize, draw, order, orderDir,  iDicTypeId);
                        break;

                    }
                case "Delete":
                    {
                        long iId = Convert.ToInt64(context.Request.Form["id"]);
                        sOut = Delete(iId);
                        break;
                    }

                case "DeleteMultiRow":
                    {
                        string sId = context.Request.Form["id"];
                        sOut = Delete(sId);
                        break;
                    }
                case "DeleteExport":
                    {
                        string sUrl = context.Request.Form["Url"];
                        Framework.Web.File.DeleteFile(Framework.Web.File.getPhysiPath(sUrl));
                        break;
                    }
                case "Add":
                    {
                        long iId = Convert.ToInt64(context.Request.Form["id"]);
                        string sTopicId = context.Request.Form["TopicId"];
                        string sSubjectId = context.Request.Form["SubjectId"];
                        string sCollection = context.Request.Form["CollectionId"];
                        string sStatus = "1";
                        try
                        {
                            sStatus = context.Request.Form["Status"];
                        }
                        catch
                        {

                        }
                        string sAllowDownload = "1";
                        try
                        {
                            sAllowDownload = context.Request.Form["AllowDownload"];
                        }
                        catch
                        {
                        }
                        string sTypeId = context.Request.Form["TypeId"];
                        string sImage = context.Request.Form["Images"];
                        long iCollectionId = 0;
                        try
                        {
                            iCollectionId = Convert.ToInt64(context.Request.Form["CollectionId"]);
                        }
                        catch
                        {
                            iCollectionId = 0;
                        }
                        long iTopicId = 0;
                        try
                        {
                            iTopicId = Convert.ToInt64(context.Request.Form["TopicId"]);
                        }
                        catch
                        {
                            iTopicId = 0;
                        }
                        long iSubjectId = 0;
                        try
                        {
                            iSubjectId = Convert.ToInt64(context.Request.Form["SubjectId"]);
                        }
                        catch
                        {
                            iSubjectId = 0;
                        }

                        long iParentId = 0;
                        try
                        {
                            iParentId = Convert.ToInt64(context.Request.Form["ParentId"]);
                        }
                        catch
                        {
                            iParentId = 0;
                        }

                        Entities.Ebook.EbookItem objEBookItem = new Entities.Ebook.EbookItem();
                        objEBookItem.Id = 0;
                        objEBookItem.PortalId = Framework.Environment.Portal.Id;
                        objEBookItem.Language = Framework.Environment.Portal.Language;
                        objEBookItem.Images = sImage;
                        try
                        {
                            objEBookItem.Status = Convert.ToInt16(sStatus);
                        }
                        catch
                        { }
                        objEBookItem.SubjectId = iSubjectId;

                        objEBookItem.CollectionId = iCollectionId;
                        try
                        {
                            objEBookItem.AllowDownload = Convert.ToInt16(sAllowDownload);
                        }
                        catch
                        { }
                        try
                        {
                            objEBookItem.Free  = Convert.ToInt16(context.Request.Form["Free"]);
                        }
                        catch
                        { }
                        
                        objEBookItem.CreatedBy = Bussiness.Common.Users.CurrentUserId();
                        objEBookItem.UpdatedBy = Bussiness.Common.Users.CurrentUserId();
                        try
                        {

                            objEBookItem.Images = context.Request.Form["Images"];
                        }
                        catch
                        {

                        }
                        try
                        {
                            objEBookItem.TypeId = Convert.ToInt64(sTypeId);
                        }
                        catch
                        {
                        }

                        try
                        {
                            objEBookItem.Language = context.Request.Form["Language"];
                        }
                        catch
                        {
                        }
                        try
                        {
                            objEBookItem.Share = Convert.ToInt16(context.Request.Form["Share"]);
                        }
                        catch
                        {
                        }
                        try
                        {
                            objEBookItem.ParentId = iParentId;
                        }
                        catch
                        {
                        }
                        Bussiness.Ebook.EbookItem objBEBookItem = new Bussiness.Ebook.EbookItem();
                        string strEbookItemId = "";
                        try
                        {
                            strEbookItemId = objBEBookItem.SaveEbookItem(objEBookItem);
                        }
                        catch
                        {

                        }
                        long lnEbookItemId = 0;
                        long lnEbookItemIdCloud = 0;
                        string[] s = strEbookItemId.Split(',');
                        try
                        {
                            lnEbookItemId = Convert.ToInt64(s[0]);
                        }
                        catch
                        {

                        }

                        sOut = "0";
                        Bussiness.Ebook.MetaDataValue objBMetaDataValue = new Bussiness.Ebook.MetaDataValue();
                        string sTitle = "";
                        try
                        {
                            sTitle = context.Request.Form["txtTitle"].ToString();
                        }
                        catch
                        {
                            sTitle = "";
                        }
                        objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, sTitle, "vi", 1, "title");
                        int iOtherTitle = 0;
                        try
                        {
                            iOtherTitle = Convert.ToInt16(context.Request.Form["hdTitle"].ToString());
                        }
                        catch
                        {

                        }

                        //Lay thong tin nhan de khac
                        for (int i = 1; i <= iOtherTitle; i++)
                        {
                            string strOtherTitle = "";
                            try
                            {
                                strOtherTitle = context.Request.Form["txtOtherTitle" + (i - 1).ToString()].ToString();
                            }
                            catch
                            {

                            }
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strOtherTitle, "vi", i, "OTHERTITLE", 0);

                        }
                        int intAuthor = 0;
                        try
                        {
                            intAuthor = Convert.ToInt16(context.Request.Form["hdAuthor"].ToString());
                        }
                        catch
                        {

                        }


                        for (int i = 1; i <= intAuthor; i++)
                        {
                            string strAuthor = "";
                            string strOldAuthor = "";
                            try
                            {
                                //strAuthor = context.Request.Form["txtFirstName" + (i - 1).ToString()].ToString() + "," + context.Request.Form["txtLastName" + (i - 1).ToString()].ToString();
                                strAuthor = context.Request.Form["txtFirstName" + (i - 1).ToString()].ToString() + " " + context.Request.Form["txtLastName" + (i - 1).ToString()].ToString(); ;
                                strOldAuthor = context.Request.Form["txtFirstName" + (i - 1).ToString()].ToString() + " " + context.Request.Form["txtLastName" + (i - 1).ToString()].ToString();
                            }
                            catch
                            {

                            }
                            if (strAuthor != "" && strAuthor != "," && strAuthor != " ")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strAuthor, "vi", i, "AUTHOR", lnEbookItemIdCloud);
                            }

                        }


                        int intNguoiHd = 0;
                        try
                        {
                            intNguoiHd = Convert.ToInt16(context.Request.Form["hdNguoiHd"].ToString());
                        }
                        catch
                        {

                        }


                        for (int i = 1; i <= intNguoiHd; i++)
                        {
                            string strAuthor = "";
                            //string strOldAuthor = "";
                            try
                            {
                                strAuthor = context.Request.Form["txtChucdanh" + (i - 1).ToString()].ToString()+" "+ context.Request.Form["txtFirstNameNguoiHd" + (i - 1).ToString()].ToString() + " " + context.Request.Form["txtLastNameNguoiHd" + (i - 1).ToString()].ToString();
                                //strAuthor = context.Request.Form["txtNguoiHd" + (i - 1).ToString()].ToString();
                                
                            }
                            catch
                            {
                                strAuthor = "";
                            }
                            if (strAuthor != "" && strAuthor != "," && strAuthor !=" ")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strAuthor, "vi", i, "NGUOIHD", lnEbookItemIdCloud);
                            }

                        }

                        //lay thong tin nha xuat ban
                        string strPublisher = "";
                        try
                        {
                            strPublisher = context.Request.Form["txtPublisher"].ToString();
                        }
                        catch
                        {
                            strPublisher = "";
                        }

                        objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strPublisher, "vi", 1, "PUBLISHER", lnEbookItemIdCloud);

                        //lay thong tin loai tai lieu

                        Bussiness.Ebook.DigType objBDigType = new Bussiness.Ebook.DigType();
                        Entities.Ebook.DigType objEDigType = objBDigType.GetDigTypeById(objEBookItem.TypeId);
                        if (objEDigType != null)
                        {

                            objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, objEDigType.Code, "vi", 1, "TYPE", lnEbookItemIdCloud);
                        }

                        //lay thong tin nam xuat ban

                        string strMonth = "";

                        try
                        {
                            strMonth = context.Request.Form["cbMonth"].ToString();
                        }
                        catch
                        {
                            strMonth = "";
                        }

                        string strDay = "";

                        try
                        {
                            strDay = context.Request.Form["txtDay"].ToString();
                        }
                        catch
                        {
                            strDay = "";
                        }
                        string strYear = "";

                        try
                        {
                            strYear = context.Request.Form["txtYear"].ToString();
                        }
                        catch
                        {
                            strYear = "";
                        }
                        if (strYear != "")
                        {
                            if (strMonth != "")
                            {
                                strMonth = strMonth.PadLeft(2, '0');
                                strYear = strYear + "-" + strMonth;
                                if (strDay != "")
                                {
                                    strDay = strDay.PadLeft(2, '0');
                                    strYear = strYear + "-" + strDay;
                                }
                            }
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strYear, "vi", 1, "PUBLISHDATE", lnEbookItemIdCloud);
                        }

                        //lay thong tin tu khoa

                        int intKeyword = 0;
                        try
                        {
                            intKeyword = Convert.ToInt16(context.Request.Form["hdKeyword"].ToString());
                        }
                        catch
                        {

                        }


                        for (int i = 1; i <= intKeyword; i++)
                        {
                            string strKeyword = "";
                            try
                            {
                                strKeyword = context.Request.Form["txtKeyword" + (i - 1).ToString()].ToString();
                            }
                            catch
                            {

                            }

                            objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strKeyword, "vi", i, "KEYWORD", lnEbookItemIdCloud);


                        }

                        //Thong tin trich dan

                        string strCitation = "";
                        try
                        {
                            strCitation = context.Request.Form["txtCitation"].ToString();
                        }
                        catch
                        {
                            strCitation = "";
                        }

                        objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strCitation, "vi", 1, "CITATION", lnEbookItemIdCloud);



                        //lay thong tin tap chi

                        int intSeries = 0;
                        try
                        {
                            intSeries = Convert.ToInt16(context.Request.Form["hdSeries"].ToString());
                        }
                        catch
                        {

                        }


                        for (int i = 1; i <= intSeries; i++)
                        {
                            string strSeries = "";
                            string strReportNo = "";
                            try
                            {
                                strSeries = context.Request.Form["txtSeries" + (i - 1).ToString()].ToString();
                                strReportNo = context.Request.Form["txtPaperNo" + (i - 1).ToString()].ToString();
                                if (strReportNo != "")
                                {
                                    strSeries = strSeries + "; " + strReportNo;
                                }
                            }
                            catch
                            {

                            }
                            if (strSeries != "" && strSeries != ";")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strSeries, "vi", i, "SERIES", lnEbookItemIdCloud);
                            }


                        }


                        int intIdentifier = 0;
                        try
                        {
                            intIdentifier = Convert.ToInt16(context.Request.Form["hdIdentifier"].ToString());
                        }
                        catch
                        {

                        }


                        for (int i = 1; i <= intIdentifier; i++)
                        {
                            string strIdentifier = "";
                            try
                            {
                                strIdentifier = context.Request.Form["txtIdentifier" + (i - 1).ToString()].ToString();
                            }
                            catch
                            {

                            }

                            string strcbIdentifier = "";
                            try
                            {
                                strcbIdentifier = context.Request.Form["cbIdentifier" + (i - 1).ToString()].ToString();
                            }
                            catch
                            {

                            }
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strIdentifier, "vi", i, strcbIdentifier.ToUpper(), lnEbookItemIdCloud);
                        }
                        //lay thong tin language

                        string sLanguage = "";
                        try
                        {
                            sLanguage = context.Request.Form["LanguageId"].ToString();
                        }
                        catch
                        {
                            sLanguage = "";
                        }

                        objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, sLanguage, "vi", 1, "LANGUAGE", lnEbookItemIdCloud);

                        //lay thong tin abstract

                        string sAbstract = "";
                        try
                        {
                            sAbstract = context.Request.Form["txtAbstract"].ToString();
                        }
                        catch
                        {
                            sAbstract = "";
                        }

                        objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, sAbstract, "vi", 1, "ABSTRACT", lnEbookItemIdCloud);


                        //lay thong tin Sponsor

                        string sSponsor = "";
                        try
                        {
                            sSponsor = context.Request.Form["txtSponsor"].ToString();
                        }
                        catch
                        {
                            sSponsor = "";
                        }

                        objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, sSponsor, "vi", 1, "SPONSORS", lnEbookItemIdCloud);


                        //lay thong tin Description

                        string sDescription = "";
                        try
                        {
                            sDescription = context.Request.Form["txtDescription"].ToString();
                        }
                        catch
                        {
                            sDescription = "";
                        }

                        objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, sDescription, "vi", 1, "DESCRIPTION", lnEbookItemIdCloud);
                        //Thong tin ngay cap nhat

                        string strSubmited = DateTime.Now.ToString("yyyy-MM-dd") + "T" + DateTime.Now.ToString("HH:mm:ss") + "Z";
                        objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strSubmited, "vi", 1, "SUBMITED", lnEbookItemIdCloud);
                        objBMetaDataValue.AddMetaDataValue(0, lnEbookItemId, strSubmited, "vi", 1, "AVAILABLE", lnEbookItemIdCloud);
                        objBEBookItem.UpdateAuthorAndKeyword(lnEbookItemId);

                        objBEBookItem.SaveLogBienMucEbook(lnEbookItemId, Bussiness.Common.Users.CurrentUserId(), "new");

                        break;
                    }
                case "Edit":
                    {
                        long iId = Convert.ToInt64(context.Request.Form["id"]);
                        sOut = Edit(iId);
                        break;
                    }
                case "SaveEdit":
                    {
                        long iId = Convert.ToInt64(context.Request.Form["id"]);
                        string sTopicId = context.Request.Form["dpTopicId"];
                        string sSubjectId = context.Request.Form["dpSubjectId"];
                        string sCollection = context.Request.Form["dpCollectionId"];
                        string sStatus = "1";
                        try
                        {
                            sStatus = context.Request.Form["chkStatus"];
                        }
                        catch
                        {

                        }
                        string sAllowDownload = "1";
                        try
                        {
                            sAllowDownload = context.Request.Form["chkAllowDownload"];
                        }
                        catch
                        {
                        }
                        string sTypeId = context.Request.Form["dpDigType"];
                        long iCollectionId = 0;
                        try
                        {
                            iCollectionId = Convert.ToInt64(context.Request.Form["dpCollectionId"]);
                        }
                        catch
                        {
                            iCollectionId = 0;
                        }
                        long iTopicId = 0;
                        try
                        {
                            iTopicId = Convert.ToInt64(context.Request.Form["dpTopicId"]);
                        }
                        catch
                        {
                            iTopicId = 0;
                        }
                        long iSubjectId = 0;
                        try
                        {
                            iSubjectId = Convert.ToInt64(context.Request.Form["dpSubjectId"]);
                        }
                        catch
                        {
                            iSubjectId = 0;
                        }

                        int iShare = 0;
                        try
                        {
                            iShare = Convert.ToInt16(context.Request.Form["dpShare"]);
                        }
                        catch
                        {
                            iShare = 0;
                        }
                       

                        string sImages = "";
                        Entities.Ebook.EbookItem objEBookItem = new Entities.Ebook.EbookItem();

                        try
                        {
                            objEBookItem.Free = Convert.ToInt16(context.Request.Form["dpFree"]);
                        }
                        catch
                        { }


                        objEBookItem.Id = iId;
                        objEBookItem.PortalId = Framework.Environment.Portal.Id;
                        objEBookItem.Language = Framework.Environment.Portal.Language;
                        objEBookItem.Images = sImages;
                        try
                        {
                            objEBookItem.Status = Convert.ToInt16(sStatus);
                        }
                        catch
                        { }
                        objEBookItem.SubjectId = iSubjectId;
                        objEBookItem.TopicId = iTopicId;
                        objEBookItem.CollectionId = iCollectionId;
                        try
                        {
                            objEBookItem.AllowDownload = Convert.ToInt16(sAllowDownload);
                        }
                        catch
                        { }
                        objEBookItem.CreatedBy = Bussiness.Common.Users.CurrentUserId();
                        objEBookItem.UpdatedBy = Bussiness.Common.Users.CurrentUserId();
                        try
                        {

                            objEBookItem.Images = context.Request.Form["dpImages"];
                        }
                        catch
                        {

                        }
                        try
                        {
                            objEBookItem.TypeId = Convert.ToInt64(sTypeId);
                        }
                        catch
                        {
                        }

                        objEBookItem.Share = iShare;

                        Bussiness.Ebook.EbookItem objBEBookItem = new Bussiness.Ebook.EbookItem();
                        string strEbookItemId = "";
                        try
                        {
                            strEbookItemId = objBEBookItem.SaveEbookItem(objEBookItem);
                        }
                        catch
                        {

                        }


                        long lnEbookItemId = 0;
                        long lnEbookItemIdCloud = 0;
                        string[] s = strEbookItemId.Split(',');
                        try
                        {
                            lnEbookItemId = Convert.ToInt64(s[0]);
                        }
                        catch
                        {

                        }
                        if (lnEbookItemId > 0)
                        {
                            foreach (string key in context.Request.Form)
                            {
                                if (key != null)
                                {
                                    if ((key.IndexOf("Field_") >= 0))
                                    {
                                        long lnId = Convert.ToInt64(key.Substring(key.IndexOf("_") + 1));
                                        string value = context.Request.Form[key].ToString();
                                        string language = "";
                                        try
                                        {
                                            language = context.Request.Form["Language_" + lnId.ToString()].ToString();
                                        }
                                        catch
                                        {

                                        }
                                        if (value.Length <= 4000)
                                        {

                                            objBMetaDataValue.SaveMetaDataValue(lnId, lnEbookItemId, 0, value, language, 1, 0);

                                        }

                                    }

                                    if (key.IndexOf("cbaddfield_") >= 0)
                                    {


                                        long id1 = Convert.ToInt64(key.Substring(key.IndexOf("_") + 1));
                                        long lnFieldId = Convert.ToInt64(context.Request.Form["cbaddfield_" + id1.ToString()]);
                                        string language = context.Request.Form["addlanguage_" + id1.ToString()].ToString();
                                        string value = context.Request.Form["txtaddfield_" + id1.ToString()];

                                        if (value.Length > 0)
                                        {

                                            if (value.Length <= 4000)
                                            {

                                                objBMetaDataValue.SaveMetaDataValue(0, lnEbookItemId, lnFieldId, value, language, 1, 0);
                                            }
                                        }
                                    }
                                }
                            }

                            sOut = "0";

                            objBEBookItem.UpdateAuthorAndKeyword(lnEbookItemId);
                            objBEBookItem.SaveLogBienMucEbook(lnEbookItemId, Bussiness.Common.Users.CurrentUserId(), "edit");
                        }
                        break;
                    }
                case "DeleteMetaDataValue":
                    {
                        sOut = "0";
                        long iId = Convert.ToInt64(context.Request.Form["id"]);
                        objBMetaDataValue.DeleteMetaDataValue(iId);
                        break;
                    }

                case "ChangeStatus":
                    {
                        try
                        {
                            long iId = Convert.ToInt64(context.Request.Form["id"]);
                            string sTypeAction = context.Request.Form["typeAction"];
                            sOut = ChangeStatus(iId, sTypeAction);
                        }
                        catch
                        {
                            sOut = "1";
                        }
                        break;
                    }
                case "CheckUserCode":
                    {
                        try
                        {
                            string sCode = context.Request.Form["Code"].ToString();
                            string sId = context.Request.Form["Id"].ToString();
                            sOut = CheckUserCode(sCode, Convert.ToInt64(sId));
                        }
                        catch
                        {
                            sOut = "false";
                        }
                        break;
                    }
                case "InitCollection":
                    {
                        sOut = InitCollection();
                        break;
                    }
               
                case "InitTapChi":
                    {
                        sOut = InitTapChi();
                        break;
                    }
                case "InitSubject":
                    {
                        sOut = InitSubject();
                        break;
                    }
                case "InitTopic":
                    {
                        sOut = InitTopic();
                        break;
                    }
                case "InitDigType":
                    {
                        sOut = InitDigType();
                        break;
                    }
                case "InitLanguage":
                    {
                        sOut = InitLanguage();
                        break;
                    }
                case "InitUser":
                    {
                        sOut = initUser();
                        break;
                    }
                case "Import":
                    {


                        string sCollectionId = context.Request.Form["CollectionId"];
                        string sTopicId = context.Request.Form["TopicId"];
                        string sSubjectId = context.Request.Form["SubjectId"];
                        string sDigTypeId = context.Request.Form["DigTypeId"];
                        string sLanguage = context.Request.Form["Language"];
                        string sFileTypeImport = context.Request.Form["fileTypeImport"];
                        long iCollectionId = 0;
                        try
                        {
                            iCollectionId = Convert.ToInt64(sCollectionId);
                        }
                        catch
                        {

                        }


                        long iTopicId = 0;
                        try
                        {
                            iTopicId = Convert.ToInt64(sTopicId);
                        }
                        catch
                        {

                        }

                        long iSubjectId = 0;
                        try
                        {
                            iSubjectId = Convert.ToInt64(sTopicId);
                        }
                        catch
                        {

                        }
                        long iDicTypeId = 0;
                        try
                        {
                            iDicTypeId = Convert.ToInt64(sDigTypeId);
                        }
                        catch
                        {

                        }

                        HttpPostedFile file = context.Request.Files["Files"];
                        if (sFileTypeImport == "Xml")
                        {
                            sOut = ImportDspaceXml(iCollectionId, iSubjectId, iTopicId, iDicTypeId, sLanguage, file);
                        }
                        if (sFileTypeImport == "Zip")
                        {
                            sOut = ImportDspaceXmlAdnContent(iCollectionId, iSubjectId, iTopicId, iDicTypeId, sLanguage, file);
                        }
                        break;
                    }
                case "InitField":
                    {

                        sOut = InitField();
                        break;
                    }
                case "ExportXmlDspaceById":
                    {
                        long iId = Convert.ToInt64(context.Request.Form["id"]);
                        sOut = ExportXmlDspaceById(iId);
                        break;
                    }
                case "ExportXmlandContentDspaceById":
                    {
                        long iId = Convert.ToInt64(context.Request.Form["id"]);
                        sOut = ExportXmlAndContentDspaceById(iId);
                        break;
                    }
                case "GetEBookInfo":
                    {
                        long iId = Convert.ToInt64(context.Request.Form["id"]);
                        sOut = GetEBookInfo(iId);
                        break;
                    }
                case "ExportXmlandContentDspace":
                    {
                        string sTitle = context.Request.Form["sTitle"];
                        string sAuthor = context.Request.Form["sAuthor"];
                        var sPublisher = context.Request.Form["sPublisher"];
                        var sSoTapChi = context.Request.Form["sSoTapChi"];
                        var sTenTapChi = context.Request.Form["sTenTapChi"];
                        var sFromYear = context.Request.Form["sFromYear"];
                        var sToYear = context.Request.Form["sToYear"];

                        string sUserId = context.Request.Form["sUserId"];
                        string sId = context.Request.Form["sId"];
                        string sCollectionId = context.Request.Form["sCollectionId"];
                        string sTopicId = context.Request.Form["sTopicId"];
                        string sSubjectId = context.Request.Form["sSubjectId"];
                        string sDigTypeId = context.Request.Form["sDigTypeId"];
                        string sKeyword = "";
                        string sStatus = context.Request.Form["sStatus"];

                        string sStartTime = context.Request.Form["sStartTime"];
                        string sEndTime = context.Request.Form["sEndTime"];
                        var dStartTime = DateTime.MinValue;
                        try
                        {
                            if (!string.IsNullOrEmpty(sStartTime))
                            {
                                dStartTime = Convert.ToDateTime(sStartTime);
                            }
                        }
                        catch
                        {

                        }
                        var dEndTime = DateTime.MinValue;
                        try
                        {
                            if (!string.IsNullOrEmpty(sEndTime))
                            {
                                dEndTime = Convert.ToDateTime(sEndTime);
                            }
                        }
                        catch
                        {

                        }
                        long iUserId = 0;
                        try
                        {
                            iUserId = Convert.ToInt64(sUserId);
                        }
                        catch
                        {

                        }
                        long iCollectionId = 0;
                        try
                        {
                            iCollectionId = Convert.ToInt64(sCollectionId);
                        }
                        catch
                        {

                        }


                        long iTopicId = 0;
                        try
                        {
                            iTopicId = Convert.ToInt64(sTopicId);
                        }
                        catch
                        {

                        }

                        long iSubjectId = 0;
                        try
                        {
                            iSubjectId = Convert.ToInt64(sSubjectId);
                        }
                        catch
                        {

                        }


                        long iId = 0;
                        try
                        {
                            iId = Convert.ToInt64(sId);
                        }
                        catch
                        {

                        }

                        long iDicTypeId = 0;
                        try
                        {
                            iDicTypeId = Convert.ToInt64(sDigTypeId);
                        }
                        catch
                        {

                        }
                        int iStatus = 0;
                        try
                        {
                            iStatus = Convert.ToInt16(iStatus);

                        }
                        catch
                        {

                        }

                        sOut = ExportXmlAndContentDspace(sTitle, sAuthor, sPublisher, "", sKeyword, Framework.Environment.DateTimeFunction.ConvertToSqlDate(sStartTime), Framework.Environment.DateTimeFunction.ConvertToSqlDate(sEndTime), iUserId, iCollectionId, iStatus, iTopicId, iSubjectId, sSoTapChi, sTenTapChi, sFromYear, sToYear);
                        break;
                    }
                case "ExportDspace":
                    {
                        sOut = "0";
                        string sTitle = context.Request.Form["sTitle"];
                        string sAuthor = context.Request.Form["sAuthor"];
                        var sPublisher = context.Request.Form["sPublisher"];
                        var sSoTapChi = context.Request.Form["sSoTapChi"];
                        var sTenTapChi = context.Request.Form["sTenTapChi"];
                        var sFromYear = context.Request.Form["sFromYear"];
                        var sToYear = context.Request.Form["sToYear"];

                        string sUserId = context.Request.Form["sUserId"];
                        string sId = context.Request.Form["sId"];
                        string sCollectionId = context.Request.Form["sCollectionId"];
                        string sTopicId = context.Request.Form["sTopicId"];
                        string sSubjectId = context.Request.Form["sSubjectId"];
                        string sDigTypeId = context.Request.Form["sDigTypeId"];
                        string sKeyword = "";
                        string sStatus = context.Request.Form["sStatus"];

                        string sStartTime = context.Request.Form["sStartTime"];
                        string sEndTime = context.Request.Form["sEndTime"];
                        var dStartTime = DateTime.MinValue;
                        try
                        {
                            if (!string.IsNullOrEmpty(sStartTime))
                            {
                                dStartTime = Convert.ToDateTime(sStartTime);
                            }
                        }
                        catch
                        {

                        }
                        var dEndTime = DateTime.MinValue;
                        try
                        {
                            if (!string.IsNullOrEmpty(sEndTime))
                            {
                                dEndTime = Convert.ToDateTime(sEndTime);
                            }
                        }
                        catch
                        {

                        }
                        long iUserId = 0;
                        try
                        {
                            iUserId = Convert.ToInt64(sUserId);
                        }
                        catch
                        {

                        }
                        long iCollectionId = 0;
                        try
                        {
                            iCollectionId = Convert.ToInt64(sCollectionId);
                        }
                        catch
                        {

                        }


                        long iTopicId = 0;
                        try
                        {
                            iTopicId = Convert.ToInt64(sTopicId);
                        }
                        catch
                        {

                        }

                        long iSubjectId = 0;
                        try
                        {
                            iSubjectId = Convert.ToInt64(sSubjectId);
                        }
                        catch
                        {

                        }


                        long iId = 0;
                        try
                        {
                            iId = Convert.ToInt64(sId);
                        }
                        catch
                        {

                        }

                        long iDicTypeId = 0;
                        try
                        {
                            iDicTypeId = Convert.ToInt64(sDigTypeId);
                        }
                        catch
                        {

                        }
                        int iStatus = 0;
                        try
                        {
                            iStatus = Convert.ToInt16(iStatus);

                        }
                        catch
                        {

                        }

                        sOut = ExportXmlDspace(sTitle, sAuthor, sPublisher, "", sKeyword, Framework.Environment.DateTimeFunction.ConvertToSqlDate(sStartTime), Framework.Environment.DateTimeFunction.ConvertToSqlDate(sEndTime), iUserId, iCollectionId, iStatus, iTopicId, iSubjectId, sSoTapChi, sTenTapChi, sFromYear, sToYear);

                        break;
                    }
                case "SetupExport":
                    {

                        sOut = SetupExport(Framework.Environment.Setting.GetKey("PathRoot") + "\\XmlConfig\\EBookExport.xml");
                        break;
                    }

                case "ExportExcel":
                    {

                        string sField = context.Request.Form["Field"];
                        string sTitle = context.Request.Form["sTitle"];
                        string sAuthor = context.Request.Form["sAuthor"];
                        var sPublisher = context.Request.Form["sPublisher"];
                        var sSoTapChi = context.Request.Form["sSoTapChi"];
                        var sTenTapChi = context.Request.Form["sTenTapChi"];
                        var sFromYear = context.Request.Form["sFromYear"];
                        var sToYear = context.Request.Form["sToYear"];

                        string sUserId = context.Request.Form["sUserId"];
                        string sId = context.Request.Form["sId"];
                        string sCollectionId = context.Request.Form["sCollectionId"];
                        string sTopicId = context.Request.Form["sTopicId"];
                        string sSubjectId = context.Request.Form["sSubjectId"];
                        string sDigTypeId = context.Request.Form["sDigTypeId"];
                        string sKeyword = "";
                        string sStatus = context.Request.Form["sStatus"];

                        string sStartTime = context.Request.Form["sStartTime"];
                        string sEndTime = context.Request.Form["sEndTime"];
                        var dStartTime = DateTime.MinValue;
                        try
                        {
                            if (!string.IsNullOrEmpty(sStartTime))
                            {
                                dStartTime = Convert.ToDateTime(sStartTime);
                            }
                        }
                        catch
                        {

                        }
                        var dEndTime = DateTime.MinValue;
                        try
                        {
                            if (!string.IsNullOrEmpty(sEndTime))
                            {
                                dEndTime = Convert.ToDateTime(sEndTime);
                            }
                        }
                        catch
                        {

                        }
                        long iUserId = 0;
                        try
                        {
                            iUserId = Convert.ToInt64(sUserId);
                        }
                        catch
                        {

                        }
                        long iCollectionId = 0;
                        try
                        {
                            iCollectionId = Convert.ToInt64(sCollectionId);
                        }
                        catch
                        {

                        }


                        long iTopicId = 0;
                        try
                        {
                            iTopicId = Convert.ToInt64(sTopicId);
                        }
                        catch
                        {

                        }

                        long iSubjectId = 0;
                        try
                        {
                            iSubjectId = Convert.ToInt64(sSubjectId);
                        }
                        catch
                        {

                        }


                        long iId = 0;
                        try
                        {
                            iId = Convert.ToInt64(sId);
                        }
                        catch
                        {

                        }

                        long iDicTypeId = 0;
                        try
                        {
                            iDicTypeId = Convert.ToInt64(sDigTypeId);
                        }
                        catch
                        {

                        }
                        int iStatus = 0;
                        try
                        {
                            iStatus = Convert.ToInt16(iStatus);

                        }
                        catch
                        {

                        }
                        sOut = ExportExcel(sTitle, sAuthor, sPublisher, "", sKeyword, Framework.Environment.DateTimeFunction.ConvertToSqlDate(sStartTime), Framework.Environment.DateTimeFunction.ConvertToSqlDate(sEndTime), iUserId, iCollectionId, iStatus, iTopicId, iSubjectId, sSoTapChi, sTenTapChi, sField, sFromYear, sToYear);


                        break;
                    }
            }


            context.Response.Write(sOut);

        }
        public string GetFileName(string XmlConfig)
        {
            string t = XmlConfig.Substring(XmlConfig.LastIndexOf("\\") + 1);
            return t;
        }
        private string SetupExport(string XmlConfig)
        {
            DataTable dtConfig = new DataTable();
            if (Framework.Environment.File.Exists(XmlConfig))
            {
                Framework.Database.Excel objBExcel = new Framework.Database.Excel();
                dtConfig = objBExcel.GetConfig(XmlConfig);


            }
            else
            {
                XmlConfig = Framework.Environment.Setting.GetPathRoot() + "\\XmlConfig\\MData\\" + GetFileName(XmlConfig);
                if (Framework.Environment.File.Exists(XmlConfig))
                {
                    Framework.Database.Excel objBExcel = new Framework.Database.Excel();
                    dtConfig = objBExcel.GetConfig(XmlConfig);

                }
                else
                {

                }
            }
            return JsonConvert.SerializeObject(
                      new
                      {

                          data = dtConfig
                      });
        }
        private string ExportExcel(string title, string author, string publisher, string publishDate, string keyword, string submitedFrom, string submitedTo, long userId, long collectionId, int status,
            long toPicId, long subjectId, string soTapChi, string tenTapChi, string field, string publishDateFrom, string publishDateTo)
        {
            if (Bussiness.Common.Users.CanAccess("Reader", Bussiness.Common.Users.CurrentUserId()))
            {
                string XmlConfig = Framework.Environment.Setting.GetKey("PathRoot") + "\\XmlConfig\\EBookExport.xml";
                DataTable ListReader = objBEBook.SearchEbook(title, author, publisher, publishDate, keyword, submitedFrom, submitedTo, userId, collectionId, status, toPicId, subjectId, publishDateFrom, publishDateTo, 0, "", "", Framework.Environment.Portal.Id, Framework.Environment.Portal.Language, 0, 0, soTapChi, tenTapChi);
                Framework.Database.Excel objBExcel = new Framework.Database.Excel();

                if (Framework.Database.Table.CheckData(ListReader))
                {
                    DataTable dtConfig = new DataTable();
                    if (Framework.Environment.File.Exists(XmlConfig))
                    {

                        dtConfig = objBExcel.GetConfig(XmlConfig);


                    }
                    else
                    {
                        XmlConfig = Framework.Environment.Setting.GetPathRoot() + "\\XmlConfig\\MData\\" + GetFileName(XmlConfig);
                        if (Framework.Environment.File.Exists(XmlConfig))
                        {

                            dtConfig = objBExcel.GetConfig(XmlConfig);

                        }
                        else
                        {

                        }
                    }
                    string[] fields = field.Split(',');
                    DataTable dtCopyConfig = dtConfig.Clone();
                    foreach (DataRow row in dtConfig.Rows)
                    {
                        if (Framework.Database.Table.CheckFieldIn(row["fieldName"].ToString(), fields))
                        {
                            row["Check"] = 1;
                            dtCopyConfig.Rows.Add(row.ItemArray);
                        }
                    }
                    string url = "\\Upload\\Temp\\" + Framework.Environment.File.getFileNameDate() + ".xlsx";
                    string fileName = Framework.Environment.Setting.GetPathRoot() + url;
                    Framework.Web.Excel objBWebExcel = new Framework.Web.Excel();
                    //    objBExcel.ExportExcel(ListReader, dtCopyConfig, XmlConfig, fileName, Bussiness.Common.SystemPara.GetSystemParaByCode("ParentLibrary").ToUpper(), Bussiness.Common.SystemPara.GetSystemParaByCode("LibraryName").ToUpper());
                    objBWebExcel.ExportExcelC2(ListReader, dtCopyConfig, XmlConfig, fileName, Bussiness.Common.SystemPara.GetSystemParaByCode("ParentLibrary").ToUpper(), Bussiness.Common.SystemPara.GetSystemParaByCode("LibraryName").ToUpper());
                    // objBExcel.ExportExcelEPlus(ListReader, XmlConfig, fileName);
                    return JsonConvert.SerializeObject(
                 new
                 {
                     data = url.Replace("\\", "/")
                 });

                }
                else
                {
                    return JsonConvert.SerializeObject(
                new
                {
                    data = ""
                });
                }

            }
            else
            {
                return "Not Permistion Access";
            }
        }
        private string ExportXmlDspace(string title, string author, string publisher, string publishDate, string keyword, string submitedFrom, string submitedTo, long userId, long collectionId, int status,
            long toPicId, long subjectId, string soTapChi, string tenTapChi, string publishDateFrom, string publishDateTo)
        {
            Bussiness.Ebook.Dspace objBDspace = new Bussiness.Ebook.Dspace();
            DataTable ListCourse = objBEBook.SearchEbook(title, author, publisher, publishDate, keyword, submitedFrom, submitedTo, userId, collectionId, status, toPicId, subjectId, publishDateFrom, publishDateTo, 0, "", "", Framework.Environment.Portal.Id, Framework.Environment.Portal.Language, 0, 0, soTapChi, tenTapChi);

            string strOutPut = objBDspace.ExportXml(ListCourse);

            System.IO.FileInfo file = new System.IO.FileInfo(Framework.Environment.Setting.GetPathRoot() + "\\Export\\" + strOutPut);

            return JsonConvert.SerializeObject(
                 new
                 {
                     data = strOutPut
                 });
        }
        private string ExportXmlDspaceById(long ebookId)
        {
            Bussiness.Ebook.Dspace objBDspace = new Bussiness.Ebook.Dspace();


            string strOutPut = objBDspace.ExportXml(ebookId);

            System.IO.FileInfo file = new System.IO.FileInfo(Framework.Environment.Setting.GetPathRoot() + "\\Export\\" + strOutPut);

            return JsonConvert.SerializeObject(
                 new
                 {
                     data = strOutPut
                 });
        }
        private string ExportXmlAndContentDspaceById(long ebookId)
        {
            Bussiness.Ebook.Dspace objBDspace = new Bussiness.Ebook.Dspace();


            string strOutPut = objBDspace.ExportXmlandContent(ebookId);
            try
            {
                DirectoryInfo d = new DirectoryInfo(Framework.Environment.Setting.GetKey("ExportDspace") + "\\item_" + ebookId.ToString());
                d.Delete(true);
            }
            catch
            { }
            return JsonConvert.SerializeObject(
                 new
                 {
                     data = strOutPut
                 });
        }

        private string ExportXmlAndContentDspace(string title, string author, string publisher, string publishDate, string keyword, string submitedFrom, string submitedTo, long userId, long collectionId, int status,
            long toPicId, long subjectId, string soTapChi, string tenTapChi, string publishDateFrom, string publishDateTo)
        {
            Bussiness.Ebook.Dspace objBDspace = new Bussiness.Ebook.Dspace();

            DataTable ListCourse = objBEBook.SearchEbook(title, author, publisher, publishDate, keyword, submitedFrom, submitedTo, userId, collectionId, status, toPicId, subjectId, publishDateFrom, publishDateTo, 0, "", "", Framework.Environment.Portal.Id, Framework.Environment.Portal.Language, 0, 0, soTapChi, tenTapChi);

            string strOutPut = objBDspace.ExportXmlandContent(ListCourse);
            try
            {
                foreach (DataRow row in ListCourse.Rows)
                {
                    DirectoryInfo d = new DirectoryInfo(Framework.Environment.Setting.GetKey("ExportDspace") + "\\item_" + row["id"].ToString());
                    d.Delete(true);
                }
            }
            catch
            { }
            return JsonConvert.SerializeObject(
                 new
                 {
                     data = strOutPut
                 });
        }
        private string ChangeStatus(long id, string typeAction)
        {
            string sOut = "0";
            try
            {
                objBEBook.ChangeEbookStatus(id, 0);
            }
            catch
            {
                sOut = "1";
            }
            return sOut;
        }
        private string SaveData(long id, string fullName, string logInName, string birthDate, string email, string address, string phone, string password, string departmentId, string postionId, string roleId, string roleWinformId, string status, string sortOrder)
        {
            string sOut = "0";
            long iUserId = Bussiness.Common.Users.CurrentUserId();
            Entities.Common.Users eSameType = new Entities.Common.Users();
            eSameType.Id = id;
            eSameType.CardNo = logInName;
            eSameType.LoginName = Framework.Environment.HtmlFunction.RemoteWhitSpace(logInName);
            eSameType.FullName = fullName;
            eSameType.Email = email;
            try
            {
                eSameType.DepartmentId = Convert.ToInt64(departmentId);
            }
            catch
            {

            }
            try
            {
                eSameType.BirthDate = Convert.ToDateTime(birthDate);
            }
            catch
            {
                eSameType.BirthDate = DateTime.MinValue;
            }
            Framework.Environment.RSACSPCls rsa = new Framework.Environment.RSACSPCls();
            eSameType.Password = rsa.Encrypt(password);
            try
            {
                eSameType.Status = Convert.ToInt16(status);
            }
            catch
            {

            }
            eSameType.Address = address;
            eSameType.Phone = phone;
            try
            {
                eSameType.RoleId = Convert.ToInt16(roleId);
            }
            catch
            {

            }
            try
            {
                eSameType.RoleWinformId = Convert.ToInt16(roleWinformId);
            }
            catch
            {

            }


            eSameType.CreatedBy = Bussiness.Common.Users.CurrentUserId();
            eSameType.CreatedDate = DateTime.Now;

            eSameType.PortalId = Framework.Environment.Portal.Id;
            eSameType.Language = Framework.Environment.Portal.Language;



            if (id == 0)
            {
                if (Bussiness.Common.Users.CanAdd("Users", iUserId))
                {
                    // objBUser.SaveUser(eSameType);
                }
                else
                {
                    sOut = "1";
                }
            }
            else
            {
                if (Bussiness.Common.Users.CanEdit("EBook", iUserId))
                {
                    // objBUser.SaveUser(eSameType);
                }
                else
                {
                    sOut = "1";
                }
            }

            return sOut;
        }
        private string Edit(long id)
        {
            try
            {
                long iUserId = Bussiness.Common.Users.CurrentUserId();

                if (Bussiness.Common.Users.CanEdit("EBook", iUserId))
                {


                    DataTable dt = objBEBook.GetDataTableEbookById(id);
                    DataTable objTable = objBMetaDataValue.GetMetaDataValue(id);
                    DataTable objMetaData = objBMetaDataFieldRegistery.GetAllMetaDataFieldRegistery(2);
                    return JsonConvert.SerializeObject(
                         new
                         {
                             data = dt,
                             metaDataValue = objTable,

                             metaDataValueRegistry = objMetaData
                         }
                       );
                }
                else
                {
                    return "Not Permistion Access";
                }


            }
            catch (Exception ex)
            {
                Framework.Web.LogFile.WriteLog(ex.Message.ToString() + "----" + Framework.Environment.DateTimeFunction.GetCurrentDateTime() + "----" + Framework.Web.Url.ClientIp() + "---Delete Action");
                return "0";
            }
        }

        private string Delete(long id)
        {
            try
            {
                if (Bussiness.Common.Users.CanEdit("EBook", Bussiness.Common.Users.CurrentUserId()))
                {
                    objBEBook.DeleteEbook(id);
                    return "0";
                }
                else
                {
                    return "1";
                }

            }
            catch (Exception ex)
            {
                Framework.Web.LogFile.WriteLog(ex.Message.ToString() + "----" + Framework.Environment.DateTimeFunction.GetCurrentDateTime() + "----" + Framework.Web.Url.ClientIp() + "---Delete Action");
                return "1";
            }
        }
        private string Delete(string id)
        {
            string sOut = "0";
            try
            {

                if (Bussiness.Common.Users.CanEdit("EBook", Bussiness.Common.Users.CurrentUserId()))
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        string[] ids = id.Split(',');
                        if (ids.Length > 0)
                        {
                            foreach (string s in ids)
                            {
                                long iId = Convert.ToInt64(s);
                                Delete(iId);

                            }
                        }
                        else
                        {
                            sOut = "1";
                        }
                    }
                    else
                    {
                        sOut = "1";
                    }


                }
                else
                {
                    return "1";
                }

            }
            catch (Exception ex)
            {
                Framework.Web.LogFile.WriteLog(ex.Message.ToString() + "----" + Framework.Environment.DateTimeFunction.GetCurrentDateTime() + "----" + Framework.Web.Url.ClientIp() + "---Delete Action");
                return "1";
            }
            return sOut;
        }
        private string Search(string title, string author, string publisher, string publishDate, string keyword, string submitedFrom, string submitedTo, long userId, long collectionId, int status,
            long toPicId, long subjectId, string soTapChi, string tenTapChi, string publishDateFrom, string publishDateTo, int indexPage, int pageSize, string draw, string order = "", string orderDir = "",long digTypeId=0)
        {

            if (Bussiness.Common.Users.CanAccess("EBook", Bussiness.Common.Users.CurrentUserId()))
            {
                //DataTable ListCourse = objBEBook.SearchEbookValuate(title, author, publisher, publishDate, keyword, submitedFrom, submitedTo, userId, collectionId, status, toPicId, subjectId, publishDateFrom, publishDateTo, 0,0,0, "", "", Framework.Environment.Portal.Id, Framework.Environment.Portal.Language, pageSize, indexPage, soTapChi, tenTapChi,digTypeId);
                //long iTotalRecord = objBEBook.GetToTalRecordEBookValuate(title, author, publisher, publishDate, keyword, submitedFrom, submitedTo, userId, collectionId, status, toPicId, subjectId, publishDateFrom, publishDateTo, 0,0,0, "", "", Framework.Environment.Portal.Id, Framework.Environment.Portal.Language, tenTapChi, soTapChi, digTypeId);

                DataTable ListCourse = objBEBook.SearchEbookHeritage(title, author, publisher, publishDate, keyword, submitedFrom, submitedTo, userId, collectionId, status, toPicId, subjectId, publishDateFrom, publishDateTo, 0, 0, 0, "", "", Framework.Environment.Portal.Id, Framework.Environment.Portal.Language, pageSize, indexPage, soTapChi, tenTapChi, digTypeId);
                long iTotalRecord = objBEBook.GetToTalRecordEBookHeritage(title, author, publisher, publishDate, keyword, submitedFrom, submitedTo, userId, collectionId, status, toPicId, subjectId, publishDateFrom, publishDateTo, 0, 0, 0, "", "", Framework.Environment.Portal.Id, Framework.Environment.Portal.Language, tenTapChi, soTapChi, digTypeId);


                return JsonConvert.SerializeObject(
                       new
                       {
                           draw = draw,
                           recordsFiltered = iTotalRecord,
                           recordsTotal = iTotalRecord,
                           data = ListCourse
                       });
            }
            else
            {
                return "Not Permistion Access";
            }
        }

        public string InitField()
        {
            DataTable objMetaData = objBMetaDataFieldRegistery.GetAllMetaDataFieldRegistery(2);

            return JsonConvert.SerializeObject(
                   new
                   {


                       data = objMetaData

                   });
        }
        public string InitCollection()
        {
            Bussiness.Ebook.Collection objB = new Bussiness.Ebook.Collection();
            DataTable ListCourse = objB.CreateDataTreeView(Framework.Environment.Portal.Id, Framework.Environment.Portal.Language, 0);
            Framework.Database.TableCls[] Fields = new Framework.Database.TableCls[] {

             new Framework.Database.TableCls("Id","long"),
               new Framework.Database.TableCls("Name","string"),


           };
            DataTable dtModule = Framework.Database.Table.CreateTable(Fields);
            DataTable dtModuleChoice = Framework.Database.Table.CreateTable(Fields);
            DataRow r = dtModule.NewRow();

            r["Id"] = 0;
            r["Name"] = Framework.Environment.Language.GetLanguage("Select Collection", "Chọn Bộ sưu tập");
            dtModuleChoice.Rows.Add(r.ItemArray);

            foreach (DataRow item in ListCourse.Rows)
            {
                r = dtModule.NewRow();
                r["Id"] = Convert.ToInt64(item["Id"].ToString());
                r["Name"] = item["Name"].ToString();
                dtModule.Rows.Add(r);
                dtModuleChoice.Rows.Add(r.ItemArray);
            }
            return JsonConvert.SerializeObject(
                   new
                   {


                       data = dtModule,
                       dataChoice = dtModuleChoice
                   });
        }

    
        public string InitSubject()
        {
            Bussiness.Ebook.Subject objB = new Bussiness.Ebook.Subject();
            DataTable ListCourse = objB.CreateDataTreeView(Framework.Environment.Portal.Id, Framework.Environment.Portal.Language);
            Framework.Database.TableCls[] Fields = new Framework.Database.TableCls[] {

             new Framework.Database.TableCls("Id","long"),
               new Framework.Database.TableCls("Name","string"),


           };
            DataTable dtModule = Framework.Database.Table.CreateTable(Fields);
            DataTable dtModuleChoice = Framework.Database.Table.CreateTable(Fields);
            DataRow r = dtModule.NewRow();

            r["Id"] = 0;
            r["Name"] =Framework.Environment.Language.GetLanguage("Select Subject", "Chọn Chủ đề");
            dtModuleChoice.Rows.Add(r.ItemArray);

            foreach (DataRow item in ListCourse.Rows)
            {
                r = dtModule.NewRow();
                r["Id"] = Convert.ToInt64(item["Id"].ToString());
                r["Name"] = item["Name"].ToString();
                dtModule.Rows.Add(r);
                dtModuleChoice.Rows.Add(r.ItemArray);
            }
            return JsonConvert.SerializeObject(
                   new
                   {


                       data = dtModule,
                       dataChoice = dtModuleChoice
                   });
        }

        public string InitTopic()
        {
            Bussiness.Ebook.Topic objB = new Bussiness.Ebook.Topic();
            DataTable ListCourse = objB.CreateDataTreeView(Framework.Environment.Portal.Id, Framework.Environment.Portal.Language, 0);
            Framework.Database.TableCls[] Fields = new Framework.Database.TableCls[] {

             new Framework.Database.TableCls("Id","long"),
               new Framework.Database.TableCls("Name","string"),


           };
            DataTable dtModule = Framework.Database.Table.CreateTable(Fields);
            DataTable dtModuleChoice = Framework.Database.Table.CreateTable(Fields);
            DataRow r = dtModule.NewRow();

            r["Id"] = 0;
            r["Name"] = Framework.Environment.Language.GetLanguage("Select Topic", "Chọn nhóm ngành"); 
            dtModuleChoice.Rows.Add(r.ItemArray);

            foreach (DataRow item in ListCourse.Rows)
            {
                r = dtModule.NewRow();
                r["Id"] = Convert.ToInt64(item["Id"].ToString());
                r["Name"] = item["Name"].ToString();
                dtModule.Rows.Add(r);
                dtModuleChoice.Rows.Add(r.ItemArray);
            }
            return JsonConvert.SerializeObject(
                   new
                   {


                       data = dtModule,
                       dataChoice = dtModuleChoice
                   });
        }
        public string InitDigType()
        {
            Bussiness.Ebook.DigType objBChucVu = new Bussiness.Ebook.DigType();
            DataTable dtSource = objBChucVu.SearchDigType("", Framework.Environment.Portal.Id, Framework.Environment.Portal.Language);
            Framework.Database.TableCls[] Fields = new Framework.Database.TableCls[] {

             new Framework.Database.TableCls("Id","long"),
               new Framework.Database.TableCls("Name","string"),


           };
            DataTable dtResult = Framework.Database.Table.CreateTable(Fields);

            
            DataTable dtModuleChoice = Framework.Database.Table.CreateTable(Fields);
            DataRow r = dtModuleChoice.NewRow();

            r["Id"] = 0;
            r["Name"] = Framework.Environment.Language.GetLanguage("Select Digital type", "Chọn loại tài liệu");
            dtModuleChoice.Rows.Add(r.ItemArray);



            if (Framework.Database.Table.CheckData(dtSource))
            {
                foreach (DataRow item in dtSource.Rows)
                {
                    r = dtResult.NewRow();
                    r["Id"] = Convert.ToInt64(item["Id"].ToString());
                    r["Name"] = item["DescriptionVn"].ToString();
                    dtResult.Rows.Add(r);
                    dtModuleChoice.Rows.Add(r.ItemArray);
                }
            }





            return JsonConvert.SerializeObject(
                   new
                   {


                       data = dtResult,
                       dataChoice = dtModuleChoice
                   });
        }


        public string InitTapChi()
        {
            Bussiness.Ebook.Ebook objBChucVu = new Bussiness.Ebook.Ebook();
            DataTable dtSource = objBChucVu.SearchMagazineByYear("",0, 0,0);
            Framework.Database.TableCls[] Fields = new Framework.Database.TableCls[] {

             new Framework.Database.TableCls("Id","long"),
               new Framework.Database.TableCls("Name","string"),


           };
            DataTable dtResult = Framework.Database.Table.CreateTable(Fields);



            DataTable dtModuleChoice = Framework.Database.Table.CreateTable(Fields);
            DataRow r = dtModuleChoice.NewRow();

            r["Id"] = 0;
            r["Name"] = Framework.Environment.Language.GetLanguage("Select Magazine", "Chọn tạp chí"); ;
            dtModuleChoice.Rows.Add(r.ItemArray);

            if (Framework.Database.Table.CheckData(dtSource))
            {
                foreach (DataRow item in dtSource.Rows)
                {
                     r = dtResult.NewRow();
                    r["Id"] = Convert.ToInt64(item["Id"].ToString());
                    r["Name"] = item["TenTapChi"].ToString() + "-" + item["PublishDate"].ToString()  ;

                    dtResult.Rows.Add(r);
                    dtModuleChoice.Rows.Add(r.ItemArray);
                }
            }





            return JsonConvert.SerializeObject(
                   new
                   {


                       data = dtResult,
                       dataChoice = dtModuleChoice
                   });
        }
        public string InitLanguage()
        {
            Bussiness.Dic.DicLanguage objBDicLanguage = new Bussiness.Dic.DicLanguage();
            List<Entities.Dic.DicLanguage> dtResult = new List<Entities.Dic.DicLanguage>();
            dtResult.Add(new Entities.Dic.DicLanguage(0, "Tiếng việt", "vie", ""));
            dtResult.Add(new Entities.Dic.DicLanguage(0, "English (United States)", "en_US", ""));
            dtResult.Add(new Entities.Dic.DicLanguage(0, "German", "de", ""));
            dtResult.Add(new Entities.Dic.DicLanguage(0, "French", "fr", ""));
            dtResult.Add(new Entities.Dic.DicLanguage(0, "Japanese", "ja", ""));
            dtResult.Add(new Entities.Dic.DicLanguage(0, "Italian", "it", ""));
            dtResult.Add(new Entities.Dic.DicLanguage(0, "Chinese", "zh", ""));




            return JsonConvert.SerializeObject(
                   new
                   {


                       data = dtResult
                   });

        }
        private string initUser()
        {
            Framework.Database.TableCls[] Fields = new Framework.Database.TableCls[] {

             new Framework.Database.TableCls("Id","long"),
               new Framework.Database.TableCls("Name","string"),


           };
            DataTable dtCategory = Framework.Database.Table.CreateTable(Fields);
            Bussiness.Common.Users objBUser = new Bussiness.Common.Users();
            DataTable dtUser = objBUser.SearchUser("", "", 0, Framework.Environment.Portal.Id, Framework.Environment.Portal.Language);
            DataRow r = dtCategory.NewRow();

            r["Id"] = 0;
            r["Name"] = "Chọn người sử dụng ";
            dtCategory.Rows.Add(r);

            foreach (DataRow item in dtUser.Rows)
            {
                r = dtCategory.NewRow();
                r["Id"] = Convert.ToInt64(item["Id"].ToString());
                r["Name"] = item["FullName"].ToString();
                dtCategory.Rows.Add(r);
            }






            return JsonConvert.SerializeObject(
                   new
                   {


                       data = dtCategory
                   });
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
        private string ImportDspaceXml(long collectionId, long subjectId, long topicId, long digTypeId, string language, HttpPostedFile file)
        {
            int iSuccess = 0;
            int iFalse = 0;
            try
            {
                //Truong hop file xml
                XmlDocument doc = new XmlDocument();
                string url = Framework.Web.File.UploadFile3(file, "Upload/Temp/", false, "");
                doc.Load(HttpContext.Current.Server.MapPath("/" + url));



                int intAllowDownload = 1;
                int intStatus = 1;




                Entities.Ebook.EbookItem objEBookItem = new Entities.Ebook.EbookItem();
                objEBookItem.Id = 0;
                objEBookItem.PortalId = Framework.Environment.Portal.Id;
                objEBookItem.Language = Framework.Environment.Portal.Language;
                objEBookItem.Status = intStatus;
                objEBookItem.SubjectId = subjectId;
                objEBookItem.TopicId = topicId;
                objEBookItem.CollectionId = collectionId;
                objEBookItem.AllowDownload = intAllowDownload;
                objEBookItem.CreatedBy = Bussiness.Common.Users.CurrentUserId();
                objEBookItem.UpdatedBy = Bussiness.Common.Users.CurrentUserId();
                try
                {
                    objEBookItem.TypeId = digTypeId;
                }
                catch
                {
                    objEBookItem.TypeId = 0;
                }
                Bussiness.Ebook.EbookItem objBEBookItem = new Bussiness.Ebook.EbookItem();
                string strEbookId = objBEBookItem.SaveEbookItem(objEBookItem);

                long lnEbookId = 0;
                try
                {
                    lnEbookId = Convert.ToInt64(strEbookId);
                }
                catch
                {

                }
                Bussiness.Ebook.MetaDataValue objBMetaDataValue = new Bussiness.Ebook.MetaDataValue();
                foreach (XmlElement xn in doc.DocumentElement)
                {


                    if (xn.Name.ToString() == "dcvalue")
                    {

                        if (xn.Attributes["element"].Value.ToString() == "contributor" && xn.Attributes["qualifier"].Value.ToString() == "author")
                        {
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "Author");
                        }
                        if (xn.Attributes["element"].Value.ToString() == "date" && xn.Attributes["qualifier"].Value.ToString() == "available")
                        {
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "available");

                        }
                        if (xn.Attributes["element"].Value.ToString() == "date" && xn.Attributes["qualifier"].Value.ToString() == "issued")
                        {
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "PUBLISHDATE");

                        }
                        if (xn.Attributes["element"].Value.ToString() == "identifier" && xn.Attributes["qualifier"].Value.ToString() == "citation")
                        {
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "citation");

                        }
                        if (xn.Attributes["element"].Value.ToString() == "identifier" && xn.Attributes["qualifier"].Value.ToString() == "issn")
                        {
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "issn");

                        }
                        if (xn.Attributes["element"].Value.ToString() == "identifier" && xn.Attributes["qualifier"].Value.ToString() == "isbn")
                        {
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "isbn");

                        }
                        if (xn.Attributes["element"].Value.ToString() == "language" && xn.Attributes["qualifier"].Value.ToString() == "iso")
                        {
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "language");

                        }
                        if (xn.Attributes["element"].Value.ToString() == "subject")
                        {
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "subject");

                        }
                        if (xn.Attributes["element"].Value.ToString() == "title")
                        {
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "title");

                        }
                        if (xn.Attributes["element"].Value.ToString() == "publisher")
                        {
                            objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "publisher");

                        }
                    }

                }

                iSuccess = iSuccess + 1;
            }
            catch (Exception ex)
            {
                iFalse = iFalse + 1;
            }
            return JsonConvert.SerializeObject(
                     new
                     {

                         success = iSuccess,
                         error = iFalse
                     });
        }

        //public void ImportDspaceXmlAdnContent(long collectionId, long subjectId, long topicId, long digTypeId, string language, HttpPostedFile file)
        //{
        //    try
        //    {
        //        //Truong hop file xml


        //        Bussiness.Ebook.Dspace objDspace = new Bussiness.Ebook.Dspace();
        //        string strFolder = Framework.Environment.File.getFileNameDate();
        //        string url = Framework.Web.File.UploadFile3(file, "Upload/Temp/", false, "");

        //        DirectoryInfo d = new DirectoryInfo(HttpContext.Current. Server.MapPath("/temp/" + strFolder));
        //        if (!d.Exists)
        //        {
        //            d.Create();
        //        }
        //        XmlDocument doc = new XmlDocument();
        //        objDspace.ExtractFile(HttpContext.Current.Server.MapPath("/" + url), d.FullName);

        //        string[] filePaths = Directory.GetFiles(d.FullName, "*.*", SearchOption.AllDirectories);
        //        //foreach (string strFile in filePaths)
        //        //{
        //        //    FileInfo f = new FileInfo(strFile);
        //        //    f.MoveTo(d.FullName + "\\" + f.Name.ToString());
        //        //}
        //        doc.Load(HttpContext.Current.Server.MapPath("/temp/" + strFolder + "/dublin_core.xml"));



        //        int intAllowDownload = 1;
        //        int intStatus = 1;




        //        Entities.Ebook.EbookItem objEBookItem = new Entities.Ebook.EbookItem();
        //        objEBookItem.Id = 0;
        //        objEBookItem.PortalId = Framework.Environment.Portal.Id;
        //        objEBookItem.Language = Framework.Environment.Portal.Language;
        //        objEBookItem.Status = intStatus;
        //        objEBookItem.SubjectId = subjectId;
        //        objEBookItem.TopicId = topicId;
        //        objEBookItem.CollectionId =collectionId;
        //        objEBookItem.AllowDownload = intAllowDownload;
        //        objEBookItem.CreatedBy = Bussiness.Common.Users.CurrentUserId();
        //        objEBookItem.UpdatedBy = Bussiness.Common.Users.CurrentUserId();
        //        try
        //        {
        //            objEBookItem.TypeId = digTypeId;
        //        }
        //        catch
        //        {
        //            objEBookItem.TypeId = 0;
        //        }
        //        Bussiness.Ebook.EbookItem objBEBookItem = new Bussiness.Ebook.EbookItem();
        //        string strEbookId = objBEBookItem.SaveEbookItem(objEBookItem);

        //        long lnEbookId = 0;
        //        try
        //        {
        //            lnEbookId = Convert.ToInt64(strEbookId);
        //        }
        //        catch
        //        {

        //        }
        //        Bussiness.Ebook.MetaDataValue objBMetaDataValue = new Bussiness.Ebook.MetaDataValue();
        //        foreach (XmlElement xn in doc.DocumentElement)
        //        {


        //            if (xn.Name.ToString() == "dcvalue")
        //            {

        //                if (xn.Attributes["element"].Value.ToString() == "contributor" && xn.Attributes["qualifier"].Value.ToString() == "author")
        //                {
        //                    objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "Author");
        //                }
        //                if (xn.Attributes["element"].Value.ToString() == "date" && xn.Attributes["qualifier"].Value.ToString() == "available")
        //                {
        //                    objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "available");

        //                }
        //                if (xn.Attributes["element"].Value.ToString() == "date" && xn.Attributes["qualifier"].Value.ToString() == "issued")
        //                {
        //                    objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "PUBLISHDATE");

        //                }
        //                if (xn.Attributes["element"].Value.ToString() == "identifier" && xn.Attributes["qualifier"].Value.ToString() == "citation")
        //                {
        //                    objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "citation");

        //                }
        //                if (xn.Attributes["element"].Value.ToString() == "identifier" && xn.Attributes["qualifier"].Value.ToString() == "issn")
        //                {
        //                    objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "issn");

        //                }
        //                if (xn.Attributes["element"].Value.ToString() == "identifier" && xn.Attributes["qualifier"].Value.ToString() == "isbn")
        //                {
        //                    objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "isbn");

        //                }
        //                if (xn.Attributes["element"].Value.ToString() == "language" && xn.Attributes["qualifier"].Value.ToString() == "iso")
        //                {
        //                    objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "language");

        //                }
        //                if (xn.Attributes["element"].Value.ToString() == "subject")
        //                {
        //                    objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "subject");

        //                }
        //                if (xn.Attributes["element"].Value.ToString() == "title")
        //                {
        //                    objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "title");

        //                }
        //                if (xn.Attributes["element"].Value.ToString() == "publisher")
        //                {
        //                    objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "publisher");

        //                }
        //            }

        //        }
        //        //Nhap EbookFile

        //        string[] lines = System.IO.File.ReadAllLines(d.FullName.ToString() + "\\contents");

        //        Bussiness.Ebook.EbookFile objBEBookFile = new Bussiness.Ebook.EbookFile();
        //        foreach (string line in lines)
        //        {
        //            // Use a tab to indent each line of the file.
        //            string strContentFile = line.Substring(0, line.IndexOf("\t"));

        //            if (strContentFile != "license.txt")
        //            {
        //                FileInfo f1 = new FileInfo(d.FullName + "\\" + strContentFile);
        //                string generatedFilename = lnEbookId + "_" + Framework.Web.File.getFileNameDate() + Framework.Web.Url.RejectMarks(strContentFile);

        //                f1.MoveTo(Framework.Web.File.getPhysiPath("/" + Framework.Environment.Setting.GetKey("uploadDocumentDir")) + "\\" + generatedFilename);

        //                float flSize = Framework.Web.File.GetFileSize(f1.FullName);
        //                string strExt = Framework.Web.File.GetExtention(f1.FullName);
        //                string strUrl = Framework.Environment.Setting.GetKey("uploadDocumentDir") + "/" + generatedFilename;

        //                Entities.Ebook.EbookFile objEbookFile = new Entities.Ebook.EbookFile();
        //                objEbookFile.EbookId = lnEbookId;
        //                objEbookFile.FileSize = flSize;
        //                objEbookFile.FileType = "Document";
        //                objEbookFile.IsConvert = 1;
        //                objEbookFile.Url = strUrl;
        //                objEbookFile.FileExt = strExt;
        //                objEbookFile.Source = f1.FullName;
        //                objEbookFile.IsDelete = 1;
        //                objEbookFile.SortOrder = 1;


        //                objBEBookFile.SaveEbookFile(objEbookFile);
        //            }
        //        }



        //        d.Delete(true);
        //        Framework.Environment.File.DeleteFile(HttpContext.Current.Server.MapPath("/" + file));
        //    }
        //    catch (Exception ex)
        //    {
        //        Framework.Web.LogFile.WriteLog(ex.Message.ToString() + "----" + Framework.Environment.DateTimeFunction.GetCurrentDateTime() + "----" + Framework.Web.Url.ClientIp() + "---Import Dspace");

        //    }
        //}
        public string ImportDspaceXmlAdnContent(long collectionId, long subjectId, long topicId, long digTypeId, string language, HttpPostedFile file)
        {

            //Truong hop file xml


            Bussiness.Ebook.Dspace objDspace = new Bussiness.Ebook.Dspace();
            string strFolder = Framework.Environment.File.getFileNameDate();
            string url = Framework.Web.File.UploadFile3(file, "Upload/Temp/", false, "");

            DirectoryInfo d = new DirectoryInfo(HttpContext.Current.Server.MapPath("/temp/" + strFolder));
            if (!d.Exists)
            {
                d.Create();
            }
            XmlDocument doc = new XmlDocument();
            objDspace.ExtractFile(HttpContext.Current.Server.MapPath("/" + url), d.FullName);

            string[] filePaths = Directory.GetFiles(d.FullName, "dublin_core.xml", SearchOption.AllDirectories);
            int iSuccess = 0;
            int iFalse = 0;
            foreach (string strFile in filePaths)
            {

                try
                {
                    doc.Load(strFile);

                    string curDir = System.IO.Path.GetDirectoryName(strFile);

                    int intAllowDownload = 1;
                    int intStatus = 1;




                    Entities.Ebook.EbookItem objEBookItem = new Entities.Ebook.EbookItem();
                    objEBookItem.Id = 0;
                    objEBookItem.PortalId = Framework.Environment.Portal.Id;
                    objEBookItem.Language = Framework.Environment.Portal.Language;
                    objEBookItem.Status = intStatus;
                    objEBookItem.SubjectId = subjectId;
                    objEBookItem.TopicId = topicId;
                    objEBookItem.CollectionId = collectionId;
                    objEBookItem.AllowDownload = intAllowDownload;
                    objEBookItem.CreatedBy = Bussiness.Common.Users.CurrentUserId();
                    objEBookItem.UpdatedBy = Bussiness.Common.Users.CurrentUserId();
                    try
                    {
                        objEBookItem.TypeId = digTypeId;
                    }
                    catch
                    {
                        objEBookItem.TypeId = 0;
                    }
                    Bussiness.Ebook.EbookItem objBEBookItem = new Bussiness.Ebook.EbookItem();
                    string strEbookId = objBEBookItem.SaveEbookItem(objEBookItem);

                    long lnEbookId = 0;
                    try
                    {
                        lnEbookId = Convert.ToInt64(strEbookId);
                    }
                    catch
                    {

                    }
                    Bussiness.Ebook.MetaDataValue objBMetaDataValue = new Bussiness.Ebook.MetaDataValue();
                    foreach (XmlElement xn in doc.DocumentElement)
                    {


                        if (xn.Name.ToString() == "dcvalue")
                        {

                            if (xn.Attributes["element"].Value.ToString() == "contributor" && xn.Attributes["qualifier"].Value.ToString() == "author")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "Author");
                            }
                            if (xn.Attributes["element"].Value.ToString() == "date" && xn.Attributes["qualifier"].Value.ToString() == "available")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "available");

                            }
                            if (xn.Attributes["element"].Value.ToString() == "date" && xn.Attributes["qualifier"].Value.ToString() == "issued")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "PUBLISHDATE");

                            }
                            if (xn.Attributes["element"].Value.ToString() == "identifier" && xn.Attributes["qualifier"].Value.ToString() == "citation")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "citation");

                            }
                            if (xn.Attributes["element"].Value.ToString() == "identifier" && xn.Attributes["qualifier"].Value.ToString() == "issn")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "issn");

                            }
                            if (xn.Attributes["element"].Value.ToString() == "identifier" && xn.Attributes["qualifier"].Value.ToString() == "isbn")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "isbn");

                            }
                            if (xn.Attributes["element"].Value.ToString() == "language" && xn.Attributes["qualifier"].Value.ToString() == "iso")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "language");

                            }
                            if (xn.Attributes["element"].Value.ToString() == "subject")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "subject");

                            }
                            if (xn.Attributes["element"].Value.ToString() == "title")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "title");

                            }
                            if (xn.Attributes["element"].Value.ToString() == "publisher")
                            {
                                objBMetaDataValue.AddMetaDataValue(0, lnEbookId, xn.InnerText, "vi", 1, "publisher");

                            }
                        }

                    }
                    //Nhap EbookFile
                    DirectoryInfo dirCurInfo = new DirectoryInfo(curDir);
                    string[] lines = System.IO.File.ReadAllLines(dirCurInfo.FullName.ToString() + "\\contents");

                    Bussiness.Ebook.EbookFile objBEBookFile = new Bussiness.Ebook.EbookFile();
                    foreach (string line in lines)
                    {
                        // Use a tab to indent each line of the file.
                        string strContentFile = line.Substring(0, line.IndexOf("\t"));

                        if (strContentFile != "license.txt")
                        {
                            FileInfo f1 = new FileInfo(dirCurInfo.FullName + "\\" + strContentFile);
                            string generatedFilename = lnEbookId + "_" + Framework.Web.File.getFileNameDate() + Framework.Web.Url.RejectMarks(strContentFile);

                            f1.MoveTo(Framework.Web.File.getPhysiPath("/" + Framework.Environment.Setting.GetKey("uploadDocumentDir")) + "\\" + generatedFilename);

                            float flSize = Framework.Web.File.GetFileSize(f1.FullName);
                            string strExt = Framework.Web.File.GetExtention(f1.FullName);
                            string strUrl = Framework.Environment.Setting.GetKey("uploadDocumentDir") + "/" + generatedFilename;

                            Entities.Ebook.EbookFile objEbookFile = new Entities.Ebook.EbookFile();
                            objEbookFile.EbookId = lnEbookId;
                            objEbookFile.FileSize = flSize;
                            objEbookFile.FileType = "Document";
                            objEbookFile.IsConvert = 1;
                            objEbookFile.Url = strUrl;
                            objEbookFile.FileExt = strExt;
                            objEbookFile.Source = f1.FullName;
                            objEbookFile.IsDelete = 1;
                            objEbookFile.SortOrder = 1;


                            objBEBookFile.SaveEbookFile(objEbookFile);
                        }
                    }
                    iSuccess = iSuccess + 1;
                }
                catch
                {
                    iFalse = iFalse + 1;
                }

            }
            return JsonConvert.SerializeObject(
                      new
                      {

                          success = iSuccess,
                          error = iFalse
                      });
        }
        private string GetEBookInfo(long Id)
        {
            Bussiness.Ebook.Ebook objBEBook = new Bussiness.Ebook.Ebook();
          DataTable dt=  objBEBook.GetDataTableEbookById(Id);
            return JsonConvert.SerializeObject(
                  new
                  {


                      data = dt
                  });
        }
        private string CheckUserCode(string code, long id)
        {
            return "false";
        }
    }
}