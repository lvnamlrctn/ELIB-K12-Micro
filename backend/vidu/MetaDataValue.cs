using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
namespace Bussiness.Ebook
{
    public class MetaDataValue
    {
        DataAccess.Ebook.MetaDataValue objDMetaDataValue = null;
        public MetaDataValue()
        {
            objDMetaDataValue = new DataAccess.Ebook.MetaDataValue();
        }
        public  MetaDataValue(string ConnectionString)
        {
            objDMetaDataValue = new DataAccess.Ebook.MetaDataValue(ConnectionString);
        }
        public string AddMetaDataValue(long Id, long ItemId, string Value, string Language, int SortOrder, string Field,long ItemIdCloud=0, string ValueUnSign = "" )
        {
            string sOut = "";
            if (! string.IsNullOrEmpty(Value))
            {
                try
                {
                    Value = Framework.Environment.HtmlFunction.RemoteWhitSpace(Value);
                }
                catch
                {

                }
                Entities.Ebook.MetaDataValue objEMetaDataValue = new Entities.Ebook.MetaDataValue();
                Entities.Ebook.MetaDataValue objEMetaDataValueCloud = new Entities.Ebook.MetaDataValue();
                DataAccess.Ebook.MetaDataValue objDMetaDataValueCloud = new DataAccess.Ebook.MetaDataValue(Framework.Environment.Portal._cloudElibConnectionString);
                switch (Field.ToUpper())
                {
                    case "TITLE":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 64;
                          sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 64;

                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "ORTHERTITLE":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 65;
                           sOut=objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 65;

                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }
                            break;
                        }
                    case "PUBLISHER":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 39;
                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 39;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;

                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "ISSN":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 23;

                           sOut= objDMetaDataValue.SaveMetaDataValueCloud(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 23;

                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "ISBN":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 20;
                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 20;

                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }

                    case "CITATION":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 18;
                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 18;

                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }


                    case "URI":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 25;
                          sOut=  objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 25;

                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "ABSTRACT":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 27;
                          sOut=  objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 27;

                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "SPONSORS":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 29;
                          sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 29;

                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "DESCRIPTION":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 26;
                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 26;

                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "PUBLISHDATE":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 15;
                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 15;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;

                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "KEYWORD":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 57;
                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 57;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;

                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "SUBMITED":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 11;
                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 11;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;

                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 82;
                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);

                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 82;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;

                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }
                            break;
                        }
                    case "AVAILABLE":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 12;

                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 12;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;

                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 74;
                          sOut=  objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);

                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 74;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;

                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }


                            break;
                        }
                    case "TYPE":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 66;
                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 66;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;

                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "LANGUAGE":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 38;
                          sOut=  objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 38;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;

                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "AUTHOR":
                        {
                            //string[] s = Value.Split(',');
                            //if (s.Length > 1)
                            //{
                            //    Value =Framework.Environment.HtmlFunction.RemoteWhitSpace(s[1].ToString()) + "," + Framework.Environment.HtmlFunction.RemoteWhitSpace(s[0].ToString());
                            //}
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 3;
                          sOut=  objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 3;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "NGUOIHD":
                        {
                            //string[] s = Value.Split(',');
                            //if (s.Length > 1)
                            //{
                            //    Value =Framework.Environment.HtmlFunction.RemoteWhitSpace(s[1].ToString()) + "," + Framework.Environment.HtmlFunction.RemoteWhitSpace(s[0].ToString());
                            //}
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 2;
                            sOut = objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 2;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;
                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }
                    case "SERIES":
                        {
                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = 43;
                           sOut= objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                            if (ItemIdCloud > 0)
                            {
                                objEMetaDataValueCloud.ItemId = ItemIdCloud;
                                objEMetaDataValueCloud.Id = Id;
                                objEMetaDataValueCloud.Value = Value;
                                objEMetaDataValueCloud.Language = Language;
                                objEMetaDataValueCloud.SortOrder = SortOrder;
                                objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                objEMetaDataValueCloud.MetaDataFieldId = 43;
                                objEMetaDataValueCloud.ItemOrgId = ItemId;

                                long iMetaDateValueOrgId = 0;
                                try
                                {
                                    iMetaDateValueOrgId = Convert.ToInt64(sOut);
                                }
                                catch
                                {
                                }
                                if (iMetaDateValueOrgId > 0)
                                {
                                    objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                                }
                            }

                            break;
                        }

                }


            }
            return sOut="";
        }
        public string SaveMetaDataValue(long Id, long ItemId, long MetaDataFieldId, string Value, string Language, int SortOrder,long Cloud=0,  string ValueUnSign = "")
        {

           
                Entities.Ebook.MetaDataValue objEMetaDataValue = new Entities.Ebook.MetaDataValue();

                            objEMetaDataValue.ItemId = ItemId;
                            objEMetaDataValue.Id = Id;
                            objEMetaDataValue.Value = Value;
                            objEMetaDataValue.Language = Language;
                            objEMetaDataValue.SortOrder = SortOrder;
                            objEMetaDataValue.ValueUnSign = ValueUnSign;
                            objEMetaDataValue.MetaDataFieldId = MetaDataFieldId;
                          string sOut=objDMetaDataValue.SaveMetaDataValue(objEMetaDataValue);
                          if (Cloud > 0)
                          {
                              long iItemMetadataValueId = 0;
                              try
                              {
                                  iItemMetadataValueId = Convert.ToInt64(sOut);
                              }
                              catch
                              {
                              }
                              if (iItemMetadataValueId > 0)
                              {
                                  DataAccess.Ebook.MetaDataValue objDMetaDataValueCloud = new DataAccess.Ebook.MetaDataValue(Framework.Environment.Portal._cloudElibConnectionString);
                                  Entities.Ebook.MetaDataValue objEMetaDataValueCloud = new Entities.Ebook.MetaDataValue();
                                  Bussiness.Ebook.EbookItem objBEbookItem=new EbookItem();
                                  long iItemCloudId= objBEbookItem.GetEbookItemCloud(ItemId);
                                  DataTable temp=objDMetaDataValueCloud.GetMetdataFieldValue(iItemMetadataValueId);
                                  long iMetadataValueCloudId=0;

                                  if (Framework.Database.Table.CheckData(temp))
                                  {
                                      try
                                      {
                                          iMetadataValueCloudId = Convert.ToInt64(temp.Rows[0]["Id"].ToString());
                                      }
                                      catch
                                      {

                                      }
                                  }

                                  objEMetaDataValueCloud.ItemId = iItemCloudId;
                                  objEMetaDataValueCloud.Id = iMetadataValueCloudId;
                                  objEMetaDataValueCloud.Value = Value.Replace("'", "''''");
                                  objEMetaDataValueCloud.Language = Language;
                                  objEMetaDataValueCloud.SortOrder = SortOrder;
                                  objEMetaDataValueCloud.ValueUnSign = ValueUnSign;
                                  objEMetaDataValueCloud.MetaDataFieldId = MetaDataFieldId;
                                  objEMetaDataValueCloud.ItemOrgId = ItemId;
                                  objEMetaDataValueCloud.MetaDataValueOrgId = iItemMetadataValueId;
                                  objDMetaDataValueCloud.SaveMetaDataValueCloud(objEMetaDataValueCloud);
                              }
                          }
                            return "";       
        }
        public DataTable GetMetaDataValue(long ItemId)
        {
            return objDMetaDataValue.GetMetaDataValue(ItemId);
        }
        public DataTable GetMetaDataValueExport(long ItemId)
        {
            return objDMetaDataValue.GetMetaDataValueExport(ItemId);
        }
        public DataTable GetMetaDataValueExport(string Ids)
        {
            return objDMetaDataValue.GetMetaDataValueExport(Ids);
        }
        public DataTable GetMetdataFieldValue(long ItemId, long FieldId)
        {
            return objDMetaDataValue.GetMetdataFieldValue(ItemId,FieldId);
        }
        public void DeleteMetaDataValue(long Id)
        {
            objDMetaDataValue.DeleteMetaDataValue(Id);
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
                    DataAccess.Ebook.MetaDataValue objDMetaDataValueCloud = new DataAccess.Ebook.MetaDataValue(Framework.Environment.Portal._cloudElibConnectionString);
                    DataTable temp = objDMetaDataValueCloud.GetMetdataFieldValue(Id);
                    long iMetadataValueCloudId = 0;

                    if (Framework.Database.Table.CheckData(temp))
                    {
                        try
                        {
                            iMetadataValueCloudId = Convert.ToInt64(temp.Rows[0]["Id"].ToString());
                        }
                        catch
                        {

                        }
                    }
                    if (iMetadataValueCloudId > 0)
                    {
                        objDMetaDataValueCloud.DeleteMetaDataValue(iMetadataValueCloudId);
                    }
                }
            }
        }
        public DataTable SearchMetaDataValue(long ItemId,int MetaDataFieldId, string Value, string Operator)
        {
            return objDMetaDataValue.SearchMetaDataValue(ItemId, MetaDataFieldId, Value, Operator);
        }
    }
}
