using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
namespace DataAccess.Ebook
{
   public class MetaDataValue
    {
       Framework.Database.SqlDatabaseHelper objSqlHelper = null;
       public MetaDataValue()
       {
           objSqlHelper = new Framework.Database.SqlDatabaseHelper();
       }
       public  MetaDataValue(string ConnectionString)
       {
           objSqlHelper = new Framework.Database.SqlDatabaseHelper(ConnectionString);
       }
        public string SaveMetaDataValue(Entities.Ebook.MetaDataValue objEMetaDataValue, int ItemCloud=0)
        {

            String[] Output = new String[10];

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",objEMetaDataValue.Id,"in"),
                new Framework.Database.DatabaseParamCls("MetaDataFieldId",objEMetaDataValue.MetaDataFieldId,"in"),
                new Framework.Database.DatabaseParamCls("Value",objEMetaDataValue.Value,"in"),
                new Framework.Database.DatabaseParamCls("value_UnSign",objEMetaDataValue.ValueUnSign,"in"),
                new Framework.Database.DatabaseParamCls("language",objEMetaDataValue.Language,"in"),
                new Framework.Database.DatabaseParamCls("sortOrder",objEMetaDataValue.SortOrder,"in"),
                new Framework.Database.DatabaseParamCls("itemId",objEMetaDataValue.ItemId,"in"),
                
                
                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {
                        
                        Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.EditMetaDataValue", arrParams, CommandType.StoredProcedure);
                        
                        break;
                    }
            }
            return Output[0].ToString();


        }
        public string SaveMetaDataValueCloud(Entities.Ebook.MetaDataValue objEMetaDataValue, int ItemCloud = 0)
        {

            String[] Output = new String[10];

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",objEMetaDataValue.Id,"in"),
                new Framework.Database.DatabaseParamCls("MetaDataFieldId",objEMetaDataValue.MetaDataFieldId,"in"),
                new Framework.Database.DatabaseParamCls("Value",objEMetaDataValue.Value,"in"),
                new Framework.Database.DatabaseParamCls("value_UnSign",objEMetaDataValue.ValueUnSign,"in"),
                new Framework.Database.DatabaseParamCls("language",objEMetaDataValue.Language,"in"),
                new Framework.Database.DatabaseParamCls("sortOrder",objEMetaDataValue.SortOrder,"in"),
                new Framework.Database.DatabaseParamCls("itemId",objEMetaDataValue.ItemId,"in"),
                new Framework.Database.DatabaseParamCls("ItemOrgId",objEMetaDataValue.ItemOrgId,"in"),
                  new Framework.Database.DatabaseParamCls("MetaDataValueOrgId",objEMetaDataValue.MetaDataValueOrgId,"in"),
                new Framework.Database.DatabaseParamCls("result","","return")
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        Output = objSqlHelper.ReturnExecuteNonQuery(null, "Ebook.EditMetaDataValue", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
            return Output[0].ToString();


        }
        public DataTable GetMetaDataValue(long ItemId)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("ItemID",ItemId,"in")
             };

            DataTable dtMetaDataValue = null;

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        dtMetaDataValue = objSqlHelper.ExecuteDataTable("Ebook.GetMetaDataValueByItemById", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }


            return dtMetaDataValue;
        }

        public DataTable GetMetaDataValueExport(long ItemId)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("ItemID",ItemId,"in")
             };

            DataTable dtMetaDataValue = null;

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        dtMetaDataValue = objSqlHelper.ExecuteDataTable("Ebook.GetExportMetaDataValueByItemById", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }


            return dtMetaDataValue;
        }
        public DataTable GetMetaDataValueExport(string Ids)
        {
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("Ids",Ids,"in")
             };

            DataTable dtMetaDataValue = null;

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        dtMetaDataValue = objSqlHelper.ExecuteDataTable("Ebook.GetExportMetaDataValue", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }


            return dtMetaDataValue;
        }
        public DataTable GetMetdataFieldValue(long ItemId,long FieldId)
        {
            DataTable dtMetaDataValue = null;

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        dtMetaDataValue = objSqlHelper.ExecuteDataTable("Select * from Ebook.MetaDataValue Where MetaDataFieldId=" + FieldId.ToString() + " and ItemId ="+ItemId.ToString() , CommandType.Text, null);
                        break;
                    }
            }


            return dtMetaDataValue;
        }
        public DataTable GetMetdataFieldValue(long MetaDataValueOrgId)
        {
          

            DataTable dtMetaDataValue = null;

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        dtMetaDataValue = objSqlHelper.ExecuteDataTable("Select * from Ebook.MetaDataValue Where MetaDataValueOrgId=" + MetaDataValueOrgId.ToString(), CommandType.Text, null);
                        break;
                    }
            }


            return dtMetaDataValue;
        }
        public DataTable SearchMetaDataValue(long ItemId,int MetaDataFieldId,string Value,string Operator)
        {
        
        
            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                                new Framework.Database.DatabaseParamCls("ItemId",ItemId,"in"),
                new Framework.Database.DatabaseParamCls("MetaDataFieldId",MetaDataFieldId,"in"),
                new Framework.Database.DatabaseParamCls("Value",Value,"in"),
                new Framework.Database.DatabaseParamCls("Operator",Operator,"in"),
             };

            DataTable dtMetaDataValue = null;

            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        dtMetaDataValue = objSqlHelper.ExecuteDataTable("Ebook.SearchMetaDataValue", CommandType.StoredProcedure, arrParams);
                        break;
                    }
            }


            return dtMetaDataValue;
        }
        public void DeleteMetaDataValue(long Id)
        {
             

            Framework.Database.DatabaseParamCls[] arrParams = new Framework.Database.DatabaseParamCls[]{
                new Framework.Database.DatabaseParamCls("id",Id,"in")               
             };
            switch (Framework.Environment.Setting.GetKey("ServerType").ToUpper())
            {
                case "SQLSERVER":
                    {

                        objSqlHelper.ExecuteQuery(null, "Ebook.DeleteMetaDataValue", arrParams, CommandType.StoredProcedure);
                        break;
                    }
            }
            
        }
    }
}
