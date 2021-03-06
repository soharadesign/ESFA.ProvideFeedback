﻿using System;
using System.Collections.Generic;

namespace ESFA.ProvideFeedback.Apprentice.Bot.Dto
{
    // <inheritdoc />
    // <summary>
    // Stores the current conversation state
    // </summary>
    public class SurveyState : Dictionary<string, object>
    {
        private const string SurveyScoreKey = "SurveyScore";
        //private const string MessagesKey = "Messages";

        public SurveyState()
        {
            this[SurveyScoreKey] = 0;
            //this[MessagesKey] = 0;
        }
        public long SurveyScore
        {
            get => (long)this[SurveyScoreKey];
            set => this[SurveyScoreKey] = value;
        }

        //public List<string> Messages
        //{
        //    get => (List<string>)this[MessagesKey];
        //    set => this[MessagesKey] = value;
        //}
    }

    //public class SurveyState
    //{
    //    public int SurveyScore { get; set; } = 0;
    //}
}