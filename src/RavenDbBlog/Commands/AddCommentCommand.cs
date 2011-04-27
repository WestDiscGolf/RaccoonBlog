﻿using System;
using System.Configuration;
using System.Web.Mvc;
using Joel.Net;
using Raven.Client;
using RavenDbBlog.Core.Models;
using RavenDbBlog.Infrastructure.Commands;
using RavenDbBlog.ViewModels;
using RazorEngine;

namespace RavenDbBlog.Commands
{
    public class AddCommentCommand : ICommand
    {
        private readonly CommentInput _commentInput;
        private readonly RequestValues _requestValues;
        private readonly int _postId;

        public IDocumentSession Session { get; set; }

        public AddCommentCommand(CommentInput commentInput, RequestValues requestValues, int postId)
        {
            _commentInput = commentInput;
            _requestValues = requestValues;
            _postId = postId;
        }

        public void Execute()
        {
            var post = Session.Load<Post>(_postId);
            var comments = Session.Load<PostComments>(_postId);

            post.CommentsCount++;

            var comment = new PostComments.Comment
            {
                Id = comments.GenerateNewCommentId(),
                Author = _commentInput.Name,
                Body = _commentInput.Body,
                CreatedAt = DateTimeOffset.Now,  
                Email = _commentInput.Email,
                Important = _requestValues.IsAuthenticated,
                Url = _commentInput.Url,
                IsSpam = CheckForSpam(),
            };

            if (comment.IsSpam)
            {
                comments.Spam.Add(comment);
            }
            else
            {
                comments.Comments.Add(comment);
            }

            var vm = new NewCommentEmailViewModel
            {
                Author = comment.Author,
                Body = MvcHtmlString.Create(comment.Body),
                CreatedAt = comment.CreatedAt,
                Email = comment.Email,
                Url = comment.Url,
            };

            var emailContents = Razor.Run(vm, "NewComment");

            CommandExcucator.ExcuteLater(new SendEmailCommand()
                                             {
                                                 Subject = "Comment: " + post.Title,
                                                 Contents = emailContents
                                             });
        }


        private bool CheckForSpam()
        {
            //Create a new instance of the Akismet API and verify your key is valid.
            string blog = "http://" + ConfigurationManager.AppSettings["MainUrl"];
			var api = new Akismet(ConfigurationManager.AppSettings["AkismetKey"], blog, _requestValues.UserAgent);
            if (!api.VerifyKey()) throw new Exception("Akismet API key invalid.");

            var akismetComment = new AkismetComment
            {
                Blog = blog,
                UserIp = _requestValues.UserHostAddress,
                UserAgent = _requestValues.UserAgent,
                CommentContent = _commentInput.Body,
                CommentType = "comment",
                CommentAuthor = _commentInput.Name,
                CommentAuthorEmail = _commentInput.Email,
                CommentAuthorUrl = _commentInput.Url,
            };

            //Check if Akismet thinks this comment is spam. Returns TRUE if spam.
            return api.CommentCheck(akismetComment);
        }
    }
}