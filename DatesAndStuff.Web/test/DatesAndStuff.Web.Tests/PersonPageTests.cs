using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace DatesAndStuff.Web.Tests;

[TestFixture]
public class PersonPageTests
{
    private IWebDriver driver;
    private StringBuilder verificationErrors;
    private const string BaseURL = "http://localhost:5091";
    private bool acceptNextAlert = true;

    private Process? _blazorProcess;

    [OneTimeSetUp]
    public void StartBlazorServer()
    {
        var webProjectPath = Path.GetFullPath(Path.Combine(
            Assembly.GetExecutingAssembly().Location,
            "../../../../../../src/DatesAndStuff.Web/DatesAndStuff.Web.csproj"
        ));

        var webProjFolderPath = Path.GetDirectoryName(webProjectPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{webProjectPath}\"", 
            WorkingDirectory = webProjFolderPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        _blazorProcess = Process.Start(startInfo);
        
        var client = new HttpClient();
        var timeout = TimeSpan.FromSeconds(30);
        var start = DateTime.Now;
        bool isServerRunning = false;

        while (DateTime.Now - start < timeout)
        {
            try
            {
                var result = client.GetAsync(BaseURL).Result;
                if (result.IsSuccessStatusCode)
                {
                    isServerRunning = true;
                    break;
                }
            }
            catch (Exception)
            {
                Thread.Sleep(1000);
            }
        }
        if (!isServerRunning)
        {
            var errorOutput = _blazorProcess?.StandardError.ReadToEnd();
            throw new Exception($"A Blazor szerver nem indult el a {BaseURL} címen 30 másodperc alatt. Háttérfolyamat hibája: {errorOutput}");
        }
    }

    [OneTimeTearDown]
    public void StopBlazorServer()
    {
        if (_blazorProcess != null && !_blazorProcess.HasExited)
        {
            _blazorProcess.Kill(true);
            _blazorProcess.Dispose();
        }
    }

    [SetUp]
    public void SetupTest()
    {
        driver = new ChromeDriver();
        verificationErrors = new StringBuilder();
    }

    [TearDown]
    public void TeardownTest()
    {
        try
        {
            driver.Quit();
            driver.Dispose();
        }
        catch (Exception)
        {
            // Ignore errors if unable to close the browser
        }
        Assert.That(verificationErrors.ToString(), Is.EqualTo(""));
    }

    [TestCase("5", 5250)]
    [TestCase("10", 5500)]
    [TestCase("0", 5000)]
    [TestCase("20", 6000)]
    [TestCase("100", 10000)]
    public void Person_SalaryIncrease_ShouldIncrease(string percentage, double expectedSalary)
    {
        // Arrange
        driver.Navigate().GoToUrl(BaseURL);
        driver.FindElement(By.XPath("//*[@data-test='PersonPageNavigation']")).Click();

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
        
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));

        // Act
        wait.Until(d =>
        {
            var input = d.FindElement(By.XPath("//*[@data-test='SalaryIncreasePercentageInput']"));
            input.Clear();
            input.SendKeys(percentage);
            return true;
        });
        
        wait.Until(d =>
        {
            var submitButton = d.FindElement(By.XPath("//*[@data-test='SalaryIncreaseSubmitButton']"));
            submitButton.Click();
            return true;
        });

        // Assert
        var salaryText = wait.Until(d =>
        {
            var label = d.FindElement(By.XPath("//*[@data-test='DisplayedSalary']"));
            
            if (string.IsNullOrWhiteSpace(label.Text))
                return null; 
                
            return label.Text;
        });

        var salaryAfterSubmission = double.Parse(salaryText);
        salaryAfterSubmission.Should().BeApproximately(expectedSalary, 0.001);
    }

    private bool IsAlertPresent()
    {
        try
        {
            driver.SwitchTo().Alert();
            return true;
        }
        catch (NoAlertPresentException)
        {
            return false;
        }
    }

    private string CloseAlertAndGetItsText()
    {
        try
        {
            IAlert alert = driver.SwitchTo().Alert();
            string alertText = alert.Text;
            if (acceptNextAlert)
            {
                alert.Accept();
            }
            else
            {
                alert.Dismiss();
            }
            return alertText;
        }
        finally
        {
            acceptNextAlert = true;
        }
    }
    
    [TestCase("-11")]
    [TestCase("-50")]
    [TestCase("-10")]
    public void Person_SalaryIncrease_NegativeValue_ShouldShowErrorMessages(string invalidPercentage)
    {
        // Arrange
        driver.Navigate().GoToUrl(BaseURL);
        driver.FindElement(By.XPath("//*[@data-test='PersonPageNavigation']")).Click();

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));

        // Act - Érték megadása
        wait.Until(d =>
        {
            var input = d.FindElement(By.XPath("//*[@data-test='SalaryIncreasePercentageInput']"));
            input.Clear();
            input.SendKeys(invalidPercentage);
            return true;
        });

        // Act - Mentés gomb
        wait.Until(d =>
        {
            var submitButton = d.FindElement(By.XPath("//*[@data-test='SalaryIncreaseSubmitButton']"));
            submitButton.Click();
            return true;
        });

        // Assert - Oldal tetején
        var topErrorMessage = wait.Until(d =>
        {
            var summary = d.FindElement(By.CssSelector(".validation-summary-errors, .validation-errors")); 
            
            if (string.IsNullOrWhiteSpace(summary.Text))
                return null;
                
            return summary.Text;
        });
        
        topErrorMessage.Should().NotBeNullOrWhiteSpace("Az oldal tetején meg kell jelennie a hibaüzenetnek.");

        // Assert - Mező alatti
        var fieldErrorMessage = wait.Until(d =>
        {
            var fieldError = d.FindElement(By.CssSelector(".validation-message")); 
            
            if (string.IsNullOrWhiteSpace(fieldError.Text))
                return null;
                
            return fieldError.Text;
        });
        
        fieldErrorMessage.Should().NotBeNullOrWhiteSpace("A beviteli mező alatt meg kell jelennie a hibaüzenetnek.");
    }
}