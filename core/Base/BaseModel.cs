﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using puck.core.Abstract;
using puck.core.Attributes;
using puck.core.Attributes.Transformers;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;
using Lucene.Net.Analysis.Core;
using puck.core.Models;
using Microsoft.AspNetCore.Mvc;

namespace puck.core.Base
{
    [Display(Name="Base Model")]
    public class BaseModel
    {
        public BaseModel() {
            Created = DateTime.Now;
            Updated = DateTime.Now;
            Id = Guid.NewGuid();
            Revision = 0;
            SortOrder = -1;
            References = new List<string>();
        }
        private dynamic _model;
        public dynamic Get() {
            if (this._model == null) {
                var modelStr = JsonConvert.SerializeObject(this);
                this._model = JsonConvert.DeserializeObject(modelStr);
            }
            return this._model;            
        }
        [UIHint("SettingsReadOnly")]
        [DefaultGUIDTransformer()]
        [IndexSettings(FieldStoreSetting = Lucene.Net.Documents.Field.Store.YES, FieldIndexSetting=Lucene.Net.Documents.Field.Index.NOT_ANALYZED,Analyzer=typeof(KeywordAnalyzer))]
        public Guid Id { get; set; }

        [UIHint("SettingsReadOnly")]
        [DefaultGUIDTransformer()]
        [IndexSettings(FieldIndexSetting = Lucene.Net.Documents.Field.Index.NOT_ANALYZED, Analyzer = typeof(KeywordAnalyzer))]
        public Guid ParentId { get; set; }
        
        [Required]
        [Display(Name="Node Name",Description = "This determines the URL, changes will update URLs of published content even if you save without publishing")]
        public String NodeName { get; set; }

        [UIHint("SettingsReadOnly")]
        [Display(Name = "Last Edited By")]
        public string LastEditedBy { get; set; }
        
        [UIHint("SettingsReadOnly")]
        [Display(Name = "Created By")]
        public string CreatedBy { get; set; }
        
        [UIHint("PuckPath")]
        [IndexSettings(LowerCaseValue = true,FieldIndexSetting = Lucene.Net.Documents.Field.Index.NOT_ANALYZED, Analyzer = typeof(KeywordAnalyzer))]
        [MaxLength(2048)]
        public string Path { get; set; }
        
        [DateTransformer()]
        [UIHint("SettingsReadOnly")]
        public DateTime Created { get; set; }

        [DateTransformer()]
        [UIHint("SettingsReadOnly")]
        public DateTime Updated { get; set; }

        [UIHint("SettingsReadOnly")]
        public int Revision { get; set; }

        [UIHint("SettingsReadOnly")]
        [IndexSettings(LowerCaseValue = true,FieldStoreSetting = Lucene.Net.Documents.Field.Store.YES,FieldIndexSetting = Lucene.Net.Documents.Field.Index.NOT_ANALYZED, Analyzer = typeof(KeywordAnalyzer))]
        [MaxLength(10)]
        public string Variant { get; set; }
        
        [UIHint("SettingsReadOnly")]
        public bool Published { get; set; }

        [Display(Name = "Sort Order")]
        [UIHint("SettingsReadOnly")]
        public int SortOrder { get; set; }

        [Required]
        [Display(Name = "Template Path")]
        [UIHint("SettingsTemplate")]
        [IndexSettings(FieldIndexSetting = Lucene.Net.Documents.Field.Index.NOT_ANALYZED, Analyzer = typeof(KeywordAnalyzer))]
        public string TemplatePath { get; set; }

        [Display(Name = "Type Chain")]
        //[UIHint("SettingsReadOnly")]
        [HiddenInput(DisplayValue = false)]
        [IndexSettings(FieldIndexSetting=Lucene.Net.Documents.Field.Index.ANALYZED,Analyzer=typeof(StandardAnalyzer),FieldStoreSetting=Lucene.Net.Documents.Field.Store.NO)]
        public string TypeChain { get; set; }

        [UIHint("SettingsType")]
        [IndexSettings(FieldIndexSetting = Lucene.Net.Documents.Field.Index.NOT_ANALYZED,Analyzer = typeof(KeywordAnalyzer), FieldStoreSetting = Lucene.Net.Documents.Field.Store.YES)]
        [MaxLength(256)]
        public string Type { get; set; }
        
        [UIHint("PuckReferences")]
        [IndexSettings(FieldIndexSetting = Lucene.Net.Documents.Field.Index.NOT_ANALYZED, Analyzer = typeof(KeywordAnalyzer))]
        public List<string> References { get; set; }
    }
}
