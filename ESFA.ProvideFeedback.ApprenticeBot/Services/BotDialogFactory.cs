﻿using System;
using System.Linq;
using System.Threading.Tasks;
using ESFA.ProvideFeedback.ApprenticeBot.Helpers;
using ESFA.ProvideFeedback.ApprenticeBot.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Prompts.Choices;
using Microsoft.Bot.Schema;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text;
using Microsoft.Bot.Builder.Prompts;

namespace ESFA.ProvideFeedback.ApprenticeBot.Services
{
    public interface IDialogFactory<T>
    {
        T BuildApprenticeFeedbackDialog();

        T BuildWelcomeDialog(T dialogs, string dialogName, IDialogStep nextStep);
        T BuildBranchingDialog(T dialogs, string dialogName, string prompt, IDialogStep positiveBranch, IDialogStep negativeBranch);
        T BuildFreeTextDialog(T dialogs, string dialogName, string prompt, IDialogStep nextStep);
        T BuildDynamicEndDialog(T dialogs, string dialogName, int requiredScore, IDialogStep positiveEnd, IDialogStep negativeEnd);
        T BuildChoicePrompt(T dialogs, string promptName, ListStyle style);
        T BuildTextPrompt(T dialogs, string promptName);
    }

    /// <summary>
    /// Factory for adding conversational dialogs to Bots
    /// </summary>
    public class BotDialogFactory : IDialogFactory<DialogSet>
    {
        private readonly ILogger<BotDialogFactory> _logger;

        public BotDialogFactory(ILogger<BotDialogFactory> log)
        {
            _logger = log;
        }

        public DialogSet BuildApprenticeFeedbackDialog()
        {
            return new DialogSet();
        }

        public DialogSet BuildWelcomeDialog(DialogSet dialogs, string dialogName, IDialogStep nextStep)
        {
            // The main form
            dialogs.Add("start",
                new WaterfallStep[]
                {
                    async (dc, args, next) =>
                    {
                        var state = ConversationState<SurveyState>.Get(dc.Context);
                        state.SurveyScore = 0;

                        foreach (string r in nextStep.Responses)
                        {
                            await AddRealisticTypingDelay(dc.Context, r);
                            await dc.Context.SendActivity(r, inputHint: InputHints.IgnoringInput);
                        }
                        await dc.Begin(nextStep.DialogTarget);
                    },
                    async (dc, args, next) =>
                    {
                        var state = ConversationState<SurveyState>.Get(dc.Context);
                        await dc.End(state);
                    }
                }
            );

            return dialogs;
        }

        public async Task AddRealisticTypingDelay(ITurnContext ctx, string textToType)
        {
            Activity typing = new Activity() { Type = ActivityTypes.Typing };
            await ctx.SendActivity(typing);
            await Task.Delay(FormHelper.CalculateTypingTimeInMs(textToType));
        }

        public DialogSet BuildBranchingDialog(DialogSet dialogs, string dialogName, string prompt, IDialogStep positiveBranch, IDialogStep negativeBranch)
        {
            dialogs.Add(dialogName, new WaterfallStep[]
            {
                async (dc, args, next) =>
                {
                    await AddRealisticTypingDelay(dc.Context, prompt);

                    await dc.Prompt("confirmationPrompt", prompt,
                        FormHelper.ConfirmationPromptOptions);
                },
                async (dc, args, next) =>
                {
                    var state = ConversationState<SurveyState>.Get(dc.Context);
                    var userState = UserState<UserState>.Get(dc.Context);

                    var positive = args["Value"] is FoundChoice response && (response.Value == "yes" ? true : false);
                    IDialogStep activeBranch;
                    
                    if (positive)
                    {
                        state.SurveyScore++;
                        activeBranch = positiveBranch;
                    }
                    else
                    {
                        state.SurveyScore--;
                        activeBranch = negativeBranch;
                    }

                    _logger.LogDebug($"{userState.UserName} has a survey score of {state.SurveyScore} which has triggered the {(positive ? "positive" : "negative")} conversation tree");

                    foreach (var r in activeBranch.Responses)
                    {
                        await AddRealisticTypingDelay(dc.Context, r);
                        await dc.Context.SendActivity(r, inputHint: InputHints.IgnoringInput);
                    }
                    await dc.Begin(activeBranch.DialogTarget);
                },
                async (dc, args, next) =>
                {
                    await dc.End();
                }
            });

            return dialogs;
        }

        public DialogSet BuildFreeTextDialog(DialogSet dialogs, string dialogName, string prompt, IDialogStep nextStep)
        {
            // A free text feedback entry prompt, with a simple echo back to the user
            dialogs.Add(dialogName, new WaterfallStep[]
            {
                async (dc, args, next) =>
                {
                    await AddRealisticTypingDelay(dc.Context, prompt);
                    await dc.Prompt("freeText", prompt);
                },
                async (dc, args, next) =>
                {
                    var state = ConversationState<SurveyState>.Get(dc.Context);
                    var userState = UserState<UserState>.Get(dc.Context);

                    var response = args["Text"];
                    _logger.LogDebug($"{userState.UserName} has wrote {response}");

                    foreach (var r in nextStep.Responses)
                    {
                        await AddRealisticTypingDelay(dc.Context, r);
                        await dc.Context.SendActivity(r, inputHint: InputHints.IgnoringInput);
                    }

                    await dc.Begin(nextStep.DialogTarget);
                },
                async (dc, args, next) =>
                {
                    await dc.End();
                }
            });

            return dialogs;
        }

        public DialogSet BuildDynamicEndDialog(DialogSet dialogs, string dialogName, int requiredScore, IDialogStep positiveEnd,
            IDialogStep negativeEnd)
        {
            dialogs.Add(dialogName,
                new WaterfallStep[]
                {
                    async (dc, args, next) =>
                    {
                        var state = ConversationState<SurveyState>.Get(dc.Context);
                        var userState = UserState<UserState>.Get(dc.Context);

                        var positive = state.SurveyScore >= requiredScore ? true : false;

                        // End the convo, deciding whether to use the positive or negative journey based on the user score
                        var activeEnd = positive ? positiveEnd : negativeEnd;

                        _logger.LogDebug($"{userState.UserName} has a survey score of {state.SurveyScore} which has triggered the {(positive ? "positive" : "negative")} ending");

                        foreach (string r in activeEnd.Responses)
                        {
                            await AddRealisticTypingDelay(dc.Context, r);
                            await dc.Context.SendActivity(r, inputHint: InputHints.IgnoringInput);
                        }

                        await dc.End(state);
                    }
                });

            return dialogs;
        }

        public DialogSet BuildChoicePrompt(DialogSet dialogs, string promptName, ListStyle style)
        {
            dialogs.Add(promptName, new Microsoft.Bot.Builder.Dialogs.ChoicePrompt(Culture.English) { Style = ListStyle.None });
            return dialogs;
        }

        public DialogSet BuildTextPrompt(DialogSet dialogs, string promptName)
        {
            dialogs.Add(promptName, new Microsoft.Bot.Builder.Dialogs.TextPrompt());
            return dialogs;
        }
    }
}