﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jint.Runtime;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Shouldly;
using Take.Blip.Builder.Actions.ExecuteScript;
using Xunit;

namespace Take.Blip.Builder.UnitTests.Actions
{
    public class ExecuteScriptActionTests : ActionTestsBase
    {
        private ExecuteScriptAction GetTarget()
        {
            return new ExecuteScriptAction();
        }

        [Fact]
        public async Task ExecuteWithSingleStatementScriptShouldSucceed()
        {
            // Arrange
            var variableName = "variable1";
            var variableValue = "my variable 1 value";
            var settings = new ExecuteScriptSettings()
            {
                Source = $"setVariable('{variableName}', '{variableValue}');"
            };
            var target = GetTarget();

            // Act
            await target.ExecuteAsync(Context, JObject.FromObject(settings), CancellationToken);

            // Assert
            await Context.Received(1).SetVariableAsync(Arg.Any<string>(), Arg.Any<string>(), CancellationToken, Arg.Any<TimeSpan>());
            await Context.Received(1).SetVariableAsync(variableName, variableValue, CancellationToken, default(TimeSpan));
        }

        [Fact]
        public async Task ExecuteWithTwoStatementScriptShouldSucceed()
        {
            // Arrange
            var number1 = "100";
            var number2 = "250";
            Context.GetVariableAsync(nameof(number1), CancellationToken).Returns(number1);
            Context.GetVariableAsync(nameof(number2), CancellationToken).Returns(number2);
            var result = "";
            
            var settings = new ExecuteScriptSettings()
            {
                Source = $@"
                    let result = parseInt(getVariable('number1')) + parseInt(getVariable('number2'));
                    setVariable('result', result);
                    "
            };
            var target = GetTarget();

            // Act
            await target.ExecuteAsync(Context, JObject.FromObject(settings), CancellationToken);

            // Assert
            await Context.Received(1).SetVariableAsync(Arg.Any<string>(), Arg.Any<string>(), CancellationToken, Arg.Any<TimeSpan>());
            await Context.Received(1).SetVariableAsync(nameof(result), "350", CancellationToken, default(TimeSpan));
        }

        [Fact]
        public async Task ExecuteWithReturnShouldSucceed()
        {
            // Arrange
            var number1 = "100";
            var number2 = "250";
            Context.GetVariableAsync(nameof(number1), CancellationToken).Returns(number1);
            Context.GetVariableAsync(nameof(number2), CancellationToken).Returns(number2);
            var result = "";

            var settings = new ExecuteScriptSettings()
            {
                Source = $@"
                    return parseInt(getVariable('number1')) + parseInt(getVariable('number2'));
                    ",
                OutputVariable = nameof(result)
            };
            var target = GetTarget();

            // Act
            await target.ExecuteAsync(Context, JObject.FromObject(settings), CancellationToken);

            // Assert
            await Context.Received(1).SetVariableAsync(Arg.Any<string>(), Arg.Any<string>(), CancellationToken, Arg.Any<TimeSpan>());
            await Context.Received(1).SetVariableAsync(nameof(result), "350", CancellationToken, default(TimeSpan));
        }

        [Fact]
        public async Task ExecuteWithJsonReturnValueShouldSucceed()
        {
            // Arrange
            var result = "{\"id\":1.0,\"valid\":true,\"options\":[1.0,2.0,3.0,3.0],\"names\":[\"a\",\"b\",\"c\",3.0],\"others\":[{\"a\":\"value1\"},{\"b\":\"value2\"},2.0],\"content\":{\"uri\":\"https://server.com/image.jpeg\",\"type\":\"image/jpeg\"}}";

            var settings = new ExecuteScriptSettings()
            {
                Source = @"
                    return {
                        id: 1,
                        valid: true,
                        options: [ 1, 2, 3 ],
                        names: [ 'a', 'b', 'c' ],
                        others: [{ a: 'value1' }, { b: 'value2' }],                        
                        content: {
                            uri: 'https://server.com/image.jpeg',
                            type: 'image/jpeg'
                        }
                    };
                    ",
                OutputVariable = nameof(result)
            };
            var target = GetTarget();

            // Act
            await target.ExecuteAsync(Context, JObject.FromObject(settings), CancellationToken);

            // Assert
            await Context.Received(1).SetVariableAsync(Arg.Any<string>(), Arg.Any<string>(), CancellationToken, Arg.Any<TimeSpan>());
            await Context.Received(1).SetVariableAsync(nameof(result), result, CancellationToken, default(TimeSpan));
        }

        [Fact]
        public async Task ExecuteWithWhileTrueShouldFail()
        {
            // Arrange            
            var result = "";

            var settings = new ExecuteScriptSettings()
            {
                Source = @"
                    var value = 0;
                    while (true) {
                        value++;
                    }
                    return value;
                    ",
                OutputVariable = nameof(result)
            };
            var target = GetTarget();

            // Act            
            try
            {
                await target.ExecuteAsync(Context, JObject.FromObject(settings), CancellationToken);
                throw new Exception("The script was executed");
            }
            catch (StatementsCountOverflowException ex)
            {
                ex.Message.ShouldBe("The maximum number of statements executed have been reached.");
            }
        }

        [Fact]
        public async Task ExecuteWithSetTimeoutShouldFail()
        {
            // Arrange            
            var result = "";

            var settings = new ExecuteScriptSettings()
            {
                Source = @"
                    setTimeout(function() {
                        return 0;
                    }, 2000);
                    ",
                OutputVariable = nameof(result)
            };
            var target = GetTarget();

            // Act            
            try
            {
                await target.ExecuteAsync(Context, JObject.FromObject(settings), CancellationToken);
                throw new Exception("The script was executed");
            }
            catch (JavaScriptException ex)
            {
                ex.Message.ShouldBe("setTimeout is not defined");
            }
        }

        [Fact]
        public async Task ExecuteScripWithXmlHttpRequestShouldFail()
        {
            // Arrange
            var settings = new ExecuteScriptSettings()
            {
                Source = @"
                    var xhr = new XMLHttpRequest();
                    xhr.onreadystatechange = function() {
                        if (xhr.readyState == XMLHttpRequest.DONE) {
                            alert(xhr.responseText);
                        }
                    }
                    xhr.open('GET', 'http://example.com', true);
                    xhr.send(null);                    
                    "
            };
            var target = GetTarget();

            // Act
            try
            {
                await target.ExecuteAsync(Context, JObject.FromObject(settings), CancellationToken);
                throw new Exception("The script was executed");
            }
            catch (JavaScriptException ex)
            {
                ex.Message.ShouldBe("XMLHttpRequest is not defined");
            }
        }
    }
}