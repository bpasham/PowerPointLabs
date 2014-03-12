﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

// TODO:  Follow these steps to enable the Ribbon (XML) item:

// 1: Copy the following code block into the ThisAddin, ThisWorkbook, or ThisDocument class.

//  protected override Microsoft.Office.Core.IRibbonExtensibility CreateRibbonExtensibilityObject()
//  {
//      return new Ribbon1();
//  }

// 2. Create callback methods in the "Ribbon Callbacks" region of this class to handle user
//    actions, such as clicking a button. Note: if you have exported this Ribbon from the Ribbon designer,
//    move your code from the event handlers to the callback methods and modify the code to work with the
//    Ribbon extensibility (RibbonX) programming model.

// 3. Assign attributes to the control tags in the Ribbon XML file to identify the appropriate callback methods in your code.  

// For more information, see the Ribbon XML documentation in the Visual Studio Tools for Office Help.


namespace PowerPointLabs
{
    [ComVisible(true)]
    public class Ribbon1 : Office.IRibbonExtensibility
    {
        private Office.IRibbonUI ribbon;
        public bool frameAnimationChecked = false;
        public bool backgroundZoomChecked = true;
        public bool spotlightDelete = true;
        public float defaultDuration = 0.5f;
        public bool startUp = false;
        public bool spotlightEnabled = false;
        public bool inSlideEnabled = false;
        public bool zoomButtonEnabled = false;
        public bool addAutoMotionEnabled = true;
        public bool reloadAutoMotionEnabled = true;
        public bool reloadSpotlight = true;

        private List<PowerPoint.MsoAnimEffect> entryEffects = new List<PowerPoint.MsoAnimEffect>()
        {
            PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimEffect.msoAnimEffectBlinds, PowerPoint.MsoAnimEffect.msoAnimEffectBox,
            PowerPoint.MsoAnimEffect.msoAnimEffectCheckerboard, PowerPoint.MsoAnimEffect.msoAnimEffectCircle, PowerPoint.MsoAnimEffect.msoAnimEffectDiamond,
            PowerPoint.MsoAnimEffect.msoAnimEffectDissolve, PowerPoint.MsoAnimEffect.msoAnimEffectFly, PowerPoint.MsoAnimEffect.msoAnimEffectPeek, 
            PowerPoint.MsoAnimEffect.msoAnimEffectPlus, PowerPoint.MsoAnimEffect.msoAnimEffectRandomBars, PowerPoint.MsoAnimEffect.msoAnimEffectSplit,
            PowerPoint.MsoAnimEffect.msoAnimEffectStrips, PowerPoint.MsoAnimEffect.msoAnimEffectWedge, PowerPoint.MsoAnimEffect.msoAnimEffectWheel,
            PowerPoint.MsoAnimEffect.msoAnimEffectWipe, PowerPoint.MsoAnimEffect.msoAnimEffectExpand, PowerPoint.MsoAnimEffect.msoAnimEffectFade,
            PowerPoint.MsoAnimEffect.msoAnimEffectFadedSwivel, PowerPoint.MsoAnimEffect.msoAnimEffectFadedZoom, PowerPoint.MsoAnimEffect.msoAnimEffectZoom,
            PowerPoint.MsoAnimEffect.msoAnimEffectCenterRevolve, PowerPoint.MsoAnimEffect.msoAnimEffectFloat, PowerPoint.MsoAnimEffect.msoAnimEffectGrowAndTurn,
            PowerPoint.MsoAnimEffect.msoAnimEffectRiseUp, PowerPoint.MsoAnimEffect.msoAnimEffectSpinner, PowerPoint.MsoAnimEffect.msoAnimEffectSwivel,
            PowerPoint.MsoAnimEffect.msoAnimEffectBoomerang, PowerPoint.MsoAnimEffect.msoAnimEffectBounce, PowerPoint.MsoAnimEffect.msoAnimEffectCredits,
            PowerPoint.MsoAnimEffect.msoAnimEffectFlip, PowerPoint.MsoAnimEffect.msoAnimEffectFloat, PowerPoint.MsoAnimEffect.msoAnimEffectPinwheel,
            PowerPoint.MsoAnimEffect.msoAnimEffectSpiral, PowerPoint.MsoAnimEffect.msoAnimEffectWhip
        };
        
        public Dictionary<String, PowerPoint.Shape> spotlightShapeMapping = new Dictionary<string,PowerPoint.Shape>();
        public Dictionary<String, PowerPoint.Slide> spotlightSlideMapping = new Dictionary<string, PowerPoint.Slide>();

        private bool _allSlides;
        private bool _previewCurrentSlide;
        private bool _captionsAllSlides;

        private IEnumerable<string> _voiceNames;

        private int _voiceSelected = 0;

        public Ribbon1()
        {
        }

        #region IRibbonExtensibility Members

        public string GetCustomUI(string ribbonID)
        {
            return GetResourceText("PowerPointLabs.Ribbon1.xml");
        }

        #endregion

        #region Ribbon Callbacks
        //Create callback methods here. For more information about adding callback methods, select the Ribbon XML item in Solution Explorer and then press F1

        public void Ribbon_Load(Office.IRibbonUI ribbonUI)
        {
            this.ribbon = ribbonUI;

            LoadVoicesIntoDropdown();
            SetCoreVoicesToSelections();
        }
        public void RefreshRibbonControl(String controlID)
        {
            try
            {
                ribbon.InvalidateControl(controlID);
            }
            catch (Exception e)
            {
                LogException(e, "RefreshRibbonControl");
                throw;
            }
        }

        private void LoadVoicesIntoDropdown()
        {
            SetVoicesFromInstalledOptions();
            RefreshVoicePicker();
        }

        private void SetVoicesFromInstalledOptions()
        {
            var installedVoices = NotesToAudio.GetVoices().ToList();
            _voiceNames = installedVoices;
        }

        private void RefreshVoicePicker()
        {
            RefreshRibbonControl("defaultVoicePicker");
        }

        //Button Click Callbacks
        private PowerPoint.Shape FindIdenticalShape(PowerPoint.Slide slideToSearch, PowerPoint.Shape shapeToSearch)
        {
            PowerPoint.Shape shapeToReturn = null;
            foreach (PowerPoint.Shape sh in slideToSearch.Shapes)
            {
                if (sh.Id == shapeToSearch.Id && sh.Name.Equals(shapeToSearch.Name))
                {
                    shapeToReturn = sh;
                    break;
                }
            }
            return shapeToReturn;
        }
        private PowerPoint.Effect GetShapeAnnimations(PowerPoint.Slide slide, PowerPoint.Shape shape)
        {
            try
            {
                PowerPoint.Sequence sequence = slide.TimeLine.MainSequence;
                PowerPoint.Effect e = null;
                for (int x = sequence.Count; x >= 1; x--)
                {
                    PowerPoint.Effect effect = sequence[x];
                    if (effect.Shape.Name == shape.Name && effect.Shape.Id == shape.Id)
                    {
                        e = effect;
                        break;
                    }
                }
                return e;
            }
            catch (Exception e)
            {
                LogException(e, "GetShapeAnimations");
                throw;
            }
        }
        public void AddInSlideAnimationButtonClick(Office.IRibbonControl control)
        {
            try
            {
                if (Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange.Count == 0)
                    return;
                InSlideAnimate.AddInSlideAnimation();
            }
            catch (Exception e)
            {
                LogException(e, "AddInSlideAnimationButtonClick");
                throw;
            }
        }
        public void AddAnimationButtonClick(Office.IRibbonControl control)
        {
            try
            {
                //Get References of current and next slides
                PowerPoint.Slide currentSlide = GetCurrentSlide();
                PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
                if (currentSlide != null && currentSlide.SlideIndex != presentation.Slides.Count)
                {
                    PowerPoint.Slide nextSlide = GetNextSlide(currentSlide);
                    AddCompleteAutoMotion(currentSlide, nextSlide);
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Please select the correct slide", "Unable to Add Animations");
                }
            }
            catch (Exception e)
            {
                LogException(e, "AddAnimationButtonClick");
                throw;
            }
        }
        public void ReloadButtonClick(Office.IRibbonControl control)
        {
            try
            {
                PowerPoint.Slide tempSlide = GetCurrentSlide();
                PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
                if (tempSlide.Name.Contains("PPSlideAnimated") && tempSlide.Name.Substring(0, 15).Equals("PPSlideAnimated"))
                {
                    PowerPoint.Slide nextSlide = presentation.Slides[tempSlide.SlideIndex + 1];
                    PowerPoint.Slide currentSlide = presentation.Slides[tempSlide.SlideIndex - 1];
                    Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(currentSlide.SlideIndex);
                    tempSlide.Delete();

                    AddCompleteAutoMotion(currentSlide, nextSlide);
                }
                else if (tempSlide.Name.Contains("PPSlideStart") && tempSlide.Name.Substring(0, 12).Equals("PPSlideStart"))
                {
                    PowerPoint.Slide animatedSlide = presentation.Slides[tempSlide.SlideIndex + 1];
                    PowerPoint.Slide finalSlide = presentation.Slides[tempSlide.SlideIndex + 2];
                    Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(tempSlide.SlideIndex);
                    animatedSlide.Delete();

                    AddCompleteAutoMotion(tempSlide, finalSlide);
                }
                else if (tempSlide.Name.Contains("PPSlideEnd") && tempSlide.Name.Substring(0, 10).Equals("PPSlideEnd"))
                {
                    PowerPoint.Slide animatedSlide = presentation.Slides[tempSlide.SlideIndex - 1];
                    PowerPoint.Slide firstSlide = presentation.Slides[tempSlide.SlideIndex - 2];
                    Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(tempSlide.SlideIndex);
                    animatedSlide.Delete();

                    AddCompleteAutoMotion(firstSlide, tempSlide);
                }
                else if (tempSlide.Name.Contains("PPSlideMulti") && tempSlide.Name.Substring(0, 12).Equals("PPSlideMulti"))
                {
                    PowerPoint.Slide startSlide1 = tempSlide;
                    PowerPoint.Slide animatedSlide1 = tempSlide;
                    PowerPoint.Slide animatedSlide2 = tempSlide;
                    PowerPoint.Slide endSlide2 = tempSlide;
                    if (tempSlide.SlideIndex > 2)
                    {
                        animatedSlide1 = presentation.Slides[tempSlide.SlideIndex - 1];
                        startSlide1 = presentation.Slides[tempSlide.SlideIndex - 2];
                        if (animatedSlide1.Name.Contains("PPSlideAnimated") && startSlide1.Name.Contains("PPSlideStart"))
                        {
                            Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(tempSlide.SlideIndex);
                            animatedSlide1.Delete();
                            AddCompleteAutoMotion(startSlide1, tempSlide);
                        }
                    }

                    if (tempSlide.SlideIndex < presentation.Slides.Count - 1)
                    {
                        animatedSlide2 = presentation.Slides[tempSlide.SlideIndex + 1];
                        endSlide2 = presentation.Slides[tempSlide.SlideIndex + 2];
                        if (animatedSlide2.Name.Contains("PPSlideAnimated") && endSlide2.Name.Contains("PPSlideEnd"))
                        {
                            Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(tempSlide.SlideIndex);
                            animatedSlide2.Delete();
                            AddCompleteAutoMotion(tempSlide, endSlide2);
                        }
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("The current slide was not added by PPTLabs AutoMotion", "Error");
                }
            }
            catch (Exception e)
            {
                LogException(e, "ReloadAnimationButtonClick");
                throw;
            }
        }
        private PowerPoint.Slide AddMagnifyingSlideWithBackground(PowerPoint.Slide currentSlide, PowerPoint.Shape selectedShape)
        {
            PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
            PowerPoint.Slide addedSlide = currentSlide.Duplicate()[1];
            MoveMotionAnimation(addedSlide);
            foreach (PowerPoint.Shape sh in currentSlide.Shapes)
            {
                PowerPoint.Shape tmp = FindIdenticalShape(addedSlide, sh);
                if (HasExitAnimation(addedSlide, tmp))
                    tmp.Delete();
            }
            addedSlide.Name = "PPTLabsMagnifyingSlide" + GetTimestamp(DateTime.Now);

            float centerX = selectedShape.Left + selectedShape.Width / 2;
            float centerY = selectedShape.Top + selectedShape.Height / 2;

            PowerPoint.Shape identicalShape = FindIdenticalShape(addedSlide, selectedShape);
            if (identicalShape != null)
            {
                identicalShape.Delete();
            }

            addedSlide.Copy();
            PowerPoint.Shape duplicatePic = addedSlide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
            duplicatePic.Name = "PPTLabsMagnifyArea" + GetTimestamp(DateTime.Now);

            duplicatePic.LockAspectRatio = Office.MsoTriState.msoFalse;
            duplicatePic.Left = 0;
            duplicatePic.Top = 0;
            duplicatePic.Width = presentation.PageSetup.SlideWidth;
            duplicatePic.Height = presentation.PageSetup.SlideHeight;

            PowerPoint.Effect effectMotion = null;
            PowerPoint.Effect effectResize = null;
            PowerPoint.Effect effectDisappear = null;

            PowerPoint.Sequence sequence = addedSlide.TimeLine.MainSequence;

            String tempFileName = Path.GetTempFileName();
            Properties.Resources.Indicator.Save(tempFileName);
            PowerPoint.Shape indicatorShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
            indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
            indicatorShape.Top = 0;
            indicatorShape.Width = 120;
            indicatorShape.Height = 84;
            indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
            effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            effectDisappear.Exit = Office.MsoTriState.msoTrue;
            effectDisappear.Timing.Duration = 0;

            foreach (PowerPoint.Shape tmp in addedSlide.Shapes)
            {
                if (!(tmp.Equals(duplicatePic) || tmp.Equals(indicatorShape)))
                {
                    DeleteShapeAnnimations(addedSlide, tmp);
                    tmp.Visible = Office.MsoTriState.msoFalse;
                }
            }

            selectedShape.Copy();
            PowerPoint.Shape magnifyShape = addedSlide.Shapes.Paste()[1];
            magnifyShape.LockAspectRatio = Office.MsoTriState.msoTrue;
            if (magnifyShape.Width > magnifyShape.Height)
                magnifyShape.Width = presentation.PageSetup.SlideWidth;
            else
                magnifyShape.Height = presentation.PageSetup.SlideHeight;

            magnifyShape.Left = (presentation.PageSetup.SlideWidth / 2) - (magnifyShape.Width / 2);
            magnifyShape.Top = (presentation.PageSetup.SlideHeight / 2) - (magnifyShape.Height / 2);

            float finalWidth = magnifyShape.Width;
            float initialWidth = selectedShape.Width;
            float finalHeight = magnifyShape.Height;
            float initialHeight = selectedShape.Height;

            float finalX = (magnifyShape.Left + (magnifyShape.Width) / 2) * (finalWidth / initialWidth);
            float initialX = (selectedShape.Left + (selectedShape.Width) / 2) * (finalWidth / initialWidth);
            float finalY = (magnifyShape.Top + (magnifyShape.Height) / 2) * (finalHeight / initialHeight);
            float initialY = (selectedShape.Top + (selectedShape.Height) / 2) * (finalHeight / initialHeight);

            magnifyShape.Delete();

            effectMotion = sequence.AddEffect(duplicatePic, PowerPoint.MsoAnimEffect.msoAnimEffectPathDown, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerAfterPrevious);
            PowerPoint.AnimationBehavior motion = effectMotion.Behaviors[1];
            effectMotion.Timing.Duration = 0.5f;
            float point1X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
            float point1Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
            float point2X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
            float point2Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
            float point3X = (finalX - initialX) / presentation.PageSetup.SlideWidth;
            float point3Y = (finalY - initialY) / presentation.PageSetup.SlideHeight;

            motion.MotionEffect.Path = "M 0 0 C " + point1X + " " + point1Y + " " + point2X + " " + point2Y + " " + point3X + " " + point3Y + " E";
            effectMotion.Timing.SmoothStart = Office.MsoTriState.msoFalse;
            effectMotion.Timing.SmoothEnd = Office.MsoTriState.msoFalse;

            effectResize = sequence.AddEffect(duplicatePic, PowerPoint.MsoAnimEffect.msoAnimEffectGrowShrink, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            PowerPoint.AnimationBehavior resize = effectResize.Behaviors[1];
            effectResize.Timing.Duration = 0.5f;
            resize.ScaleEffect.ByX = (finalWidth / initialWidth) * 100;
            resize.ScaleEffect.ByY = (finalHeight / initialHeight) * 100;

            indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
            addedSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoTrue;
            addedSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoFalse;
            addedSlide.SlideShowTransition.AdvanceTime = 0;
            addedSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
            if (addedSlide.HasNotesPage == Office.MsoTriState.msoTrue)
            {
                foreach (PowerPoint.Shape tmp in addedSlide.NotesPage.Shapes)
                {
                    if (tmp.TextFrame.HasText == Office.MsoTriState.msoTrue)
                        tmp.TextEffect.Text = "";
                }
            }

            foreach (PowerPoint.Shape tmp in addedSlide.Shapes)
            {
                if (tmp.Type == Office.MsoShapeType.msoMedia)
                    tmp.Delete();
            }
            return addedSlide;
        }
        private PowerPoint.Slide AddMagnifiedSlideWithBackground(PowerPoint.Slide magnifyingSlide, PowerPoint.Shape selectedShape)
        {
            PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
            PowerPoint.Slide addedSlide = magnifyingSlide.Duplicate()[1];
            addedSlide.Name = "PPTLabsMagnifiedSlide" + GetTimestamp(DateTime.Now);
            PowerPoint.Shape duplicatePic = null;
            foreach (PowerPoint.Shape sh in magnifyingSlide.Shapes)
            {
                PowerPoint.Shape tmp = FindIdenticalShape(addedSlide, sh);
                if (!sh.Name.Contains("PPTLabsMagnifyArea"))
                {
                    tmp.Delete();
                }
                else
                {
                    duplicatePic = tmp;
                }
            }

            DeleteShapeAnnimations(addedSlide, duplicatePic);
            selectedShape.Copy();
            PowerPoint.Shape magnifyShape = addedSlide.Shapes.Paste()[1];
            magnifyShape.LockAspectRatio = Office.MsoTriState.msoTrue;
            if (magnifyShape.Width > magnifyShape.Height)
                magnifyShape.Width = presentation.PageSetup.SlideWidth;
            else
                magnifyShape.Height = presentation.PageSetup.SlideHeight;

            magnifyShape.Left = (presentation.PageSetup.SlideWidth / 2) - (magnifyShape.Width / 2);
            magnifyShape.Top = (presentation.PageSetup.SlideHeight / 2) - (magnifyShape.Height / 2);

            float finalWidth = magnifyShape.Width;
            float initialWidth = selectedShape.Width;
            float finalHeight = magnifyShape.Height;
            float initialHeight = selectedShape.Height;

            selectedShape.Copy();
            PowerPoint.Shape magnifyShape2 = addedSlide.Shapes.Paste()[1];
            magnifyShape2.Left = selectedShape.Left;
            magnifyShape2.Top = selectedShape.Top;
            magnifyShape2.Width = selectedShape.Width;
            magnifyShape2.Height = selectedShape.Height;

            Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(addedSlide.SlideIndex);
            duplicatePic.Select();
            magnifyShape2.Select(Office.MsoTriState.msoFalse);
            PowerPoint.ShapeRange selection = Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange;
            PowerPoint.Shape groupShape = selection.Group();

            groupShape.Width *= (finalWidth / initialWidth);
            groupShape.Height *= (finalHeight / initialHeight);
            groupShape.Ungroup();
            duplicatePic.Left += (magnifyShape.Left - magnifyShape2.Left);
            duplicatePic.Top += (magnifyShape.Top - magnifyShape2.Top);
            magnifyShape.Delete();
            magnifyShape2.Delete();

            PowerPoint.Effect effectDisappear = null;
            PowerPoint.Sequence sequence = addedSlide.TimeLine.MainSequence;
            String tempFileName = Path.GetTempFileName();
            Properties.Resources.Indicator.Save(tempFileName);
            PowerPoint.Shape indicatorShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
            indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
            indicatorShape.Top = 0;
            indicatorShape.Width = 120;
            indicatorShape.Height = 84;
            indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
            effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            effectDisappear.Exit = Office.MsoTriState.msoTrue;
            effectDisappear.Timing.Duration = 0;

            indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
            addedSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
            addedSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoFalse;
            addedSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoTrue;
            return addedSlide;
        }
        private PowerPoint.Slide AddDeMagnifyingSlideWithBackground(PowerPoint.Slide magnifyingSlide, PowerPoint.Shape selectedShape)
        {
            PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
            PowerPoint.Slide addedSlide = magnifyingSlide.Duplicate()[1];
            addedSlide.Name = "PPTLabsDeMagnifyingSlide" + GetTimestamp(DateTime.Now);
            addedSlide.MoveTo(magnifyingSlide.SlideIndex + 2);

            PowerPoint.Shape duplicatePic = null;
            foreach (PowerPoint.Shape sh in magnifyingSlide.Shapes)
            {
                PowerPoint.Shape tmp = FindIdenticalShape(addedSlide, sh);
                DeleteShapeAnnimations(addedSlide, tmp);
                if (sh.Name.Contains("PPTLabsMagnifyArea"))
                {
                    duplicatePic = tmp;
                }
                if (sh.Name.Contains("PPIndicator"))
                {
                    tmp.Delete();
                }
            }

            selectedShape.Copy();
            PowerPoint.Shape magnifyShape = addedSlide.Shapes.Paste()[1];
            magnifyShape.LockAspectRatio = Office.MsoTriState.msoTrue;
            if (magnifyShape.Width > magnifyShape.Height)
                magnifyShape.Width = presentation.PageSetup.SlideWidth;
            else
                magnifyShape.Height = presentation.PageSetup.SlideHeight;

            magnifyShape.Left = (presentation.PageSetup.SlideWidth / 2) - (magnifyShape.Width / 2);
            magnifyShape.Top = (presentation.PageSetup.SlideHeight / 2) - (magnifyShape.Height / 2);

            float finalWidthMagnify = magnifyShape.Width;
            float initialWidthMagnify = selectedShape.Width;
            float finalHeightMagnify = magnifyShape.Height;
            float initialHeightMagnify = selectedShape.Height;

            selectedShape.Copy();
            PowerPoint.Shape magnifyShape2 = addedSlide.Shapes.Paste()[1];
            magnifyShape2.Left = selectedShape.Left;
            magnifyShape2.Top = selectedShape.Top;
            magnifyShape2.Width = selectedShape.Width;
            magnifyShape2.Height = selectedShape.Height;

            Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(addedSlide.SlideIndex);
            duplicatePic.Select();
            magnifyShape2.Select(Office.MsoTriState.msoFalse);
            PowerPoint.ShapeRange selection = Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange;
            PowerPoint.Shape groupShape = selection.Group();

            groupShape.Width *= (finalWidthMagnify / initialWidthMagnify);
            groupShape.Height *= (finalHeightMagnify / initialHeightMagnify);
            groupShape.Ungroup();
            duplicatePic.Left += (magnifyShape.Left - magnifyShape2.Left);
            duplicatePic.Top += (magnifyShape.Top - magnifyShape2.Top);
            magnifyShape.Delete();
            magnifyShape2.Delete();

            PowerPoint.Effect effectDisappear = null;
            PowerPoint.Effect effectFade = null;

            PowerPoint.Sequence sequence = addedSlide.TimeLine.MainSequence;
            String tempFileName = Path.GetTempFileName();
            Properties.Resources.Indicator.Save(tempFileName);
            PowerPoint.Shape indicatorShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
            indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
            indicatorShape.Top = 0;
            indicatorShape.Width = 120;
            indicatorShape.Height = 84;
            indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
            effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            effectDisappear.Exit = Office.MsoTriState.msoTrue;
            effectDisappear.Timing.Duration = 0;

            float finalWidth = presentation.PageSetup.SlideWidth;
            float initialWidth = duplicatePic.Width;
            float finalHeight = presentation.PageSetup.SlideHeight;
            float initialHeight = duplicatePic.Height;

            float finalX = presentation.PageSetup.SlideWidth / 2;
            float initialX = (duplicatePic.Left + (duplicatePic.Width) / 2);
            float finalY = presentation.PageSetup.SlideHeight / 2;
            float initialY = (duplicatePic.Top + (duplicatePic.Height) / 2);

            int numFrames = 10;

            float incrementWidth = ((finalWidth / initialWidth) - 1.0f) / numFrames;
            float incrementHeight = ((finalHeight / initialHeight) - 1.0f) / numFrames;
            //float incrementRotation = GetMinimumRotation(initialRotation, finalRotation) / numFrames;
            float incrementLeft = (finalX - initialX) / numFrames;
            float incrementTop = (finalY - initialY) / numFrames;

            PowerPoint.Shape lastShape = duplicatePic;
            for (int i = 1; i <= numFrames; i++)
            {
                PowerPoint.Shape dupShape = duplicatePic.Duplicate()[1];
                if (i != 1)
                    sequence[sequence.Count].Delete();

                dupShape.LockAspectRatio = Office.MsoTriState.msoFalse;
                dupShape.Left = duplicatePic.Left;
                dupShape.Top = duplicatePic.Top;
                //dupShape.Rotation = groupShape.Rotation;

                if (incrementWidth != 0.0f)
                {
                    dupShape.ScaleWidth((1.0f + (incrementWidth * i)), Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromMiddle);
                }

                if (incrementHeight != 0.0f)
                {
                    dupShape.ScaleHeight((1.0f + (incrementHeight * i)), Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromMiddle);
                }

                //if (incrementRotation != 0.0f)
                //{
                //    dupShape.Rotation += (incrementRotation * i);
                //}

                if (incrementLeft != 0.0f)
                {
                    dupShape.Left += (incrementLeft * i);
                }

                if (incrementTop != 0.0f)
                {
                    dupShape.Top += (incrementTop * i);
                }

                PowerPoint.Effect appear = sequence.AddEffect(dupShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                //appear.Timing.Duration = 0.005f;
                appear.Timing.TriggerDelayTime = ((defaultDuration / numFrames) * i);

                PowerPoint.Effect disappear = sequence.AddEffect(lastShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                disappear.Exit = Office.MsoTriState.msoTrue;
                //disappear.Timing.Duration = 0.005f;
                disappear.Timing.TriggerDelayTime = ((defaultDuration / numFrames) * i);

                lastShape = dupShape;
            }

            int j = 0;
            foreach (PowerPoint.Shape tmp in addedSlide.Shapes)
            {
                if (!(tmp.Equals(duplicatePic) || tmp.Equals(indicatorShape)) && !(tmp.Name.Contains("PPTLabsMagnifyShape")) && !(tmp.Name.Contains("PPTLabsMagnifyArea")))
                {
                    tmp.Visible = Office.MsoTriState.msoTrue;
                    if (j == 0)
                    {
                        effectFade = sequence.AddEffect(tmp, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerAfterPrevious);
                    }
                    else
                    {
                        effectFade = sequence.AddEffect(tmp, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                    }
                    effectFade.Timing.Duration = 0.01f;
                    j++;
                }
            }
            effectFade = sequence.AddEffect(lastShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            effectFade.Exit = Office.MsoTriState.msoTrue;
            effectFade.Timing.Duration = 0.01f;

            indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
            addedSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
            addedSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoFalse;
            addedSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoTrue;

            return addedSlide;
        }
        private PowerPoint.Slide AddMagnifyingSlide(PowerPoint.Slide currentSlide, PowerPoint.Shape selectedShape)
        {
            PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
            PowerPoint.Slide addedSlide = currentSlide.Duplicate()[1];
            MoveMotionAnimation(addedSlide);
            foreach (PowerPoint.Shape sh in currentSlide.Shapes)
            {
                PowerPoint.Shape tmp = FindIdenticalShape(addedSlide, sh);
                if (HasExitAnimation(addedSlide, tmp))
                    tmp.Delete();
            }
            addedSlide.Name = "PPTLabsMagnifyingSlide" + GetTimestamp(DateTime.Now);

            float centerX = selectedShape.Left + selectedShape.Width / 2;
            float centerY = selectedShape.Top + selectedShape.Height / 2;

            PowerPoint.Shape identicalShape = FindIdenticalShape(addedSlide, selectedShape);
            if (identicalShape != null)
            {
                identicalShape.Delete();
            }

            addedSlide.Copy();
            PowerPoint.Shape duplicatePic = addedSlide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
            duplicatePic.Name = "PPTLabsMagnifyArea" + GetTimestamp(DateTime.Now);

            float scaleFactorX = presentation.PageSetup.SlideWidth / duplicatePic.Width;
            float scaleFactorY = presentation.PageSetup.SlideHeight / duplicatePic.Height;

            duplicatePic.LockAspectRatio = Office.MsoTriState.msoFalse;
            duplicatePic.Left = 0;
            duplicatePic.Top = 0;
            duplicatePic.Width = presentation.PageSetup.SlideWidth;
            duplicatePic.Height = presentation.PageSetup.SlideHeight;

            duplicatePic.PictureFormat.CropLeft += selectedShape.Left / scaleFactorX;
            duplicatePic.PictureFormat.CropTop += selectedShape.Top / scaleFactorY;
            duplicatePic.PictureFormat.CropRight += (presentation.PageSetup.SlideWidth - (selectedShape.Left + selectedShape.Width)) / scaleFactorX;
            duplicatePic.PictureFormat.CropBottom += (presentation.PageSetup.SlideHeight - (selectedShape.Top + selectedShape.Height)) / scaleFactorY;

            //duplicatePic.Cut();

            //currentSlide.Duplicate();
            //Globals.ThisAddIn.Application.ActivePresentation.Slides.AddSlide(currentSlide.SlideIndex + 1, Globals.ThisAddIn.Application.ActivePresentation.SlideMaster.CustomLayouts[7]);
            //PowerPoint.Slide addedSlide = GetNextSlide(currentSlide);

            //Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(addedSlide.SlideIndex);
            //PowerPoint.Shape sh = addedSlide.Shapes.Paste()[1];
            duplicatePic.Left = centerX - (duplicatePic.Width / 2);
            duplicatePic.Top = centerY - (duplicatePic.Height / 2);

            PowerPoint.Effect effectMotion = null;
            PowerPoint.Effect effectResize = null;
            PowerPoint.Effect effectDisappear = null;
            PowerPoint.Effect effectFade = null;

            PowerPoint.Sequence sequence = addedSlide.TimeLine.MainSequence;

            String tempFileName = Path.GetTempFileName();
            Properties.Resources.Indicator.Save(tempFileName);
            PowerPoint.Shape indicatorShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
            indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
            indicatorShape.Top = 0;
            indicatorShape.Width = 120;
            indicatorShape.Height = 84;
            indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
            effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            effectDisappear.Exit = Office.MsoTriState.msoTrue;
            effectDisappear.Timing.Duration = 0;

            foreach (PowerPoint.Shape tmp in addedSlide.Shapes)
            {
                if (!(tmp.Equals(duplicatePic) || tmp.Equals(indicatorShape)))
                {
                    DeleteShapeAnnimations(addedSlide, tmp);
                    effectFade = sequence.AddEffect(tmp, PowerPoint.MsoAnimEffect.msoAnimEffectFade, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                    effectFade.Exit = Office.MsoTriState.msoTrue;
                    effectFade.Timing.Duration = 0.25f;
                }
            }

            duplicatePic.Copy();
            PowerPoint.Shape magnifyShape = addedSlide.Shapes.Paste()[1];
            magnifyShape.LockAspectRatio = Office.MsoTriState.msoTrue;
            if (magnifyShape.Width > magnifyShape.Height)
            {
                magnifyShape.Width = presentation.PageSetup.SlideWidth;
            }
            else
            {
                magnifyShape.Height = presentation.PageSetup.SlideHeight;
            }

            magnifyShape.Left = (presentation.PageSetup.SlideWidth / 2) - (magnifyShape.Width / 2);
            magnifyShape.Top = (presentation.PageSetup.SlideHeight / 2) - (magnifyShape.Height / 2);

            float finalX = (magnifyShape.Left + (magnifyShape.Width) / 2);
            float initialX = (duplicatePic.Left + (duplicatePic.Width) / 2);
            float finalY = (magnifyShape.Top + (magnifyShape.Height) / 2);
            float initialY = (duplicatePic.Top + (duplicatePic.Height) / 2);

            float finalWidth = magnifyShape.Width;
            float initialWidth = duplicatePic.Width;
            float finalHeight = magnifyShape.Height;
            float initialHeight = duplicatePic.Height;

            magnifyShape.Delete();

            effectMotion = sequence.AddEffect(duplicatePic, PowerPoint.MsoAnimEffect.msoAnimEffectPathDown, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerAfterPrevious);
            PowerPoint.AnimationBehavior motion = effectMotion.Behaviors[1];
            effectMotion.Timing.Duration = 0.5f;
            float point1X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
            float point1Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
            float point2X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
            float point2Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
            float point3X = (finalX - initialX) / presentation.PageSetup.SlideWidth;
            float point3Y = (finalY - initialY) / presentation.PageSetup.SlideHeight;

            motion.MotionEffect.Path = "M 0 0 C " + point1X + " " + point1Y + " " + point2X + " " + point2Y + " " + point3X + " " + point3Y + " E";
            effectMotion.Timing.SmoothStart = Office.MsoTriState.msoFalse;
            effectMotion.Timing.SmoothEnd = Office.MsoTriState.msoFalse;

            effectResize = sequence.AddEffect(duplicatePic, PowerPoint.MsoAnimEffect.msoAnimEffectGrowShrink, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            PowerPoint.AnimationBehavior resize = effectResize.Behaviors[1];
            effectResize.Timing.Duration = 0.5f;
            resize.ScaleEffect.ByX = (finalWidth / initialWidth) * 100;
            resize.ScaleEffect.ByY = (finalHeight / initialHeight) * 100;

            indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
            addedSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoTrue;
            addedSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoFalse;
            addedSlide.SlideShowTransition.AdvanceTime = 0;
            addedSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
            if (addedSlide.HasNotesPage == Office.MsoTriState.msoTrue)
            {
                foreach (PowerPoint.Shape tmp in addedSlide.NotesPage.Shapes)
                {
                    if (tmp.TextFrame.HasText == Office.MsoTriState.msoTrue)
                        tmp.TextEffect.Text = "";
                }
            }

            foreach (PowerPoint.Shape tmp in addedSlide.Shapes)
            {
                if (tmp.Type == Office.MsoShapeType.msoMedia)
                    tmp.Delete();
            }
            return addedSlide;
        }
        private PowerPoint.Slide AddMagnifiedSlide(PowerPoint.Slide magnifyingSlide)
        {
            PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
            PowerPoint.Slide addedSlide = magnifyingSlide.Duplicate()[1];
            addedSlide.Name = "PPTLabsMagnifiedSlide" + GetTimestamp(DateTime.Now);
            PowerPoint.Shape magnifyShape = null;
            foreach (PowerPoint.Shape sh in magnifyingSlide.Shapes)
            {
                PowerPoint.Shape tmp = FindIdenticalShape(addedSlide, sh);
                if (!sh.Name.Contains("PPTLabsMagnifyArea"))
                {
                    tmp.Delete();
                }
                else
                {
                    magnifyShape = tmp;
                }
            }

            DeleteShapeAnnimations(addedSlide, magnifyShape);
            magnifyShape.LockAspectRatio = Office.MsoTriState.msoTrue;
            //magnifyShape.Left = 0;
            //magnifyShape.Top = 0;
            if (magnifyShape.Width > magnifyShape.Height)
                magnifyShape.Width = presentation.PageSetup.SlideWidth;
            else
                magnifyShape.Height = presentation.PageSetup.SlideHeight;

            magnifyShape.Left = (presentation.PageSetup.SlideWidth / 2) - (magnifyShape.Width / 2);
            magnifyShape.Top = (presentation.PageSetup.SlideHeight / 2) - (magnifyShape.Height / 2);

            PowerPoint.Effect effectDisappear = null;
            PowerPoint.Sequence sequence = addedSlide.TimeLine.MainSequence;
            String tempFileName = Path.GetTempFileName();
            Properties.Resources.Indicator.Save(tempFileName);
            PowerPoint.Shape indicatorShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
            indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
            indicatorShape.Top = 0;
            indicatorShape.Width = 120;
            indicatorShape.Height = 84;
            indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
            effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            effectDisappear.Exit = Office.MsoTriState.msoTrue;
            effectDisappear.Timing.Duration = 0;

            indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
            addedSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
            addedSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoFalse;
            addedSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoTrue;
            return addedSlide;
        }
        private PowerPoint.Slide AddDeMagnifyingSlide(PowerPoint.Slide magnifyingSlide, PowerPoint.Shape selectedShape)
        {
            PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
            PowerPoint.Slide addedSlide = magnifyingSlide.Duplicate()[1];
            addedSlide.Name = "PPTLabsDeMagnifyingSlide" + GetTimestamp(DateTime.Now);
            addedSlide.MoveTo(magnifyingSlide.SlideIndex + 2);

            PowerPoint.Shape magnifyShape = null;
            foreach (PowerPoint.Shape sh in magnifyingSlide.Shapes)
            {
                PowerPoint.Shape tmp = FindIdenticalShape(addedSlide, sh);
                DeleteShapeAnnimations(addedSlide, tmp);
                if (sh.Name.Contains("PPTLabsMagnifyArea"))
                {
                    magnifyShape = tmp;
                }
                if (sh.Name.Contains("PPIndicator"))
                {
                    tmp.Delete();
                }
            }

            magnifyShape.LockAspectRatio = Office.MsoTriState.msoTrue;
            if (magnifyShape.Width > magnifyShape.Height)
                magnifyShape.Width = presentation.PageSetup.SlideWidth;
            else
                magnifyShape.Height = presentation.PageSetup.SlideHeight;

            magnifyShape.Left = (presentation.PageSetup.SlideWidth / 2) - (magnifyShape.Width / 2);
            magnifyShape.Top = (presentation.PageSetup.SlideHeight / 2) - (magnifyShape.Height / 2);

            PowerPoint.Effect effectDisappear = null;
            PowerPoint.Effect effectMotion = null;
            PowerPoint.Effect effectResize = null;
            PowerPoint.Effect effectFade = null;

            PowerPoint.Sequence sequence = addedSlide.TimeLine.MainSequence;
            String tempFileName = Path.GetTempFileName();
            Properties.Resources.Indicator.Save(tempFileName);
            PowerPoint.Shape indicatorShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
            indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
            indicatorShape.Top = 0;
            indicatorShape.Width = 120;
            indicatorShape.Height = 84;
            indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
            effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            effectDisappear.Exit = Office.MsoTriState.msoTrue;
            effectDisappear.Timing.Duration = 0;

            float finalX = (selectedShape.Left + (selectedShape.Width) / 2);
            float initialX = (magnifyShape.Left + (magnifyShape.Width) / 2);
            float finalY = (selectedShape.Top + (selectedShape.Height) / 2);
            float initialY = (magnifyShape.Top + (magnifyShape.Height) / 2);

            float finalWidth = selectedShape.Width;
            float initialWidth = magnifyShape.Width;
            float finalHeight = selectedShape.Height;
            float initialHeight = magnifyShape.Height;

            effectMotion = sequence.AddEffect(magnifyShape, PowerPoint.MsoAnimEffect.msoAnimEffectPathDown, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            PowerPoint.AnimationBehavior motion = effectMotion.Behaviors[1];
            effectMotion.Timing.Duration = 0.5f;
            float point1X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
            float point1Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
            float point2X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
            float point2Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
            float point3X = (finalX - initialX) / presentation.PageSetup.SlideWidth;
            float point3Y = (finalY - initialY) / presentation.PageSetup.SlideHeight;

            motion.MotionEffect.Path = "M 0 0 C " + point1X + " " + point1Y + " " + point2X + " " + point2Y + " " + point3X + " " + point3Y + " E";
            effectMotion.Timing.SmoothStart = Office.MsoTriState.msoFalse;
            effectMotion.Timing.SmoothEnd = Office.MsoTriState.msoFalse;

            effectResize = sequence.AddEffect(magnifyShape, PowerPoint.MsoAnimEffect.msoAnimEffectGrowShrink, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            PowerPoint.AnimationBehavior resize = effectResize.Behaviors[1];
            effectResize.Timing.Duration = 0.5f;
            resize.ScaleEffect.ByX = (finalWidth / initialWidth) * 100;
            resize.ScaleEffect.ByY = (finalHeight / initialHeight) * 100;

            int i = 0;
            foreach (PowerPoint.Shape tmp in addedSlide.Shapes)
            {
                if (!(tmp.Equals(magnifyShape) || tmp.Equals(indicatorShape)))
                {
                    if (i == 0)
                    {
                        effectFade = sequence.AddEffect(tmp, PowerPoint.MsoAnimEffect.msoAnimEffectFade, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerAfterPrevious);
                    }
                    else
                    {
                        effectFade = sequence.AddEffect(tmp, PowerPoint.MsoAnimEffect.msoAnimEffectFade, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                    }
                    effectFade.Timing.Duration = 0.25f;
                    i++;
                }
            }
            effectFade = sequence.AddEffect(magnifyShape, PowerPoint.MsoAnimEffect.msoAnimEffectFade, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            effectFade.Exit = Office.MsoTriState.msoTrue;
            effectFade.Timing.Duration = 0.25f;

            indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
            addedSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
            addedSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoFalse;
            addedSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoTrue;

            return addedSlide;
        }
        public void ZoomBtnClick(Office.IRibbonControl control)
        {
            try
            {
                PowerPoint.Slide currentSlide = GetCurrentSlide();
                if (Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange.Count == 0)
                    return;

                PowerPoint.Shape selectedShape2 = Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange[1];
                selectedShape2.Name = "PPTLabsMagnifyShape" + GetTimestamp(DateTime.Now);
                selectedShape2.Copy();
                selectedShape2.Visible = Office.MsoTriState.msoFalse;
                PowerPoint.Shape selectedShape = currentSlide.Shapes.Paste()[1];
                selectedShape.LockAspectRatio = Office.MsoTriState.msoFalse;

                if (selectedShape2.Width > selectedShape2.Height)
                {
                    selectedShape.Width = selectedShape2.Width;
                    selectedShape.Height = Globals.ThisAddIn.Application.ActivePresentation.PageSetup.SlideHeight * selectedShape.Width / Globals.ThisAddIn.Application.ActivePresentation.PageSetup.SlideWidth;
                    selectedShape.Left = selectedShape2.Left + (selectedShape2.Width / 2) - (selectedShape.Width / 2);
                    selectedShape.Top = selectedShape2.Top + (selectedShape2.Height / 2) - (selectedShape.Height / 2);
                }
                else
                {
                    selectedShape.Height = selectedShape2.Height;
                    selectedShape.Width = Globals.ThisAddIn.Application.ActivePresentation.PageSetup.SlideWidth * selectedShape.Height / Globals.ThisAddIn.Application.ActivePresentation.PageSetup.SlideHeight;
                    selectedShape.Left = selectedShape2.Left + (selectedShape2.Width / 2) - (selectedShape.Width / 2);
                    selectedShape.Top = selectedShape2.Top + (selectedShape2.Height / 2) - (selectedShape.Height / 2);
                }


                if (!backgroundZoomChecked)
                {
                    PowerPoint.Slide magnifyingSlide = AddMagnifyingSlide(currentSlide, selectedShape);
                    PowerPoint.Slide magnifiedSlide = AddMagnifiedSlide(magnifyingSlide);
                    PowerPoint.Slide demagnifyingSlide = AddDeMagnifyingSlide(magnifyingSlide, selectedShape);

                    Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(magnifyingSlide.SlideIndex);
                    Globals.ThisAddIn.Application.CommandBars.ExecuteMso("AnimationPreview");
                }
                else
                {
                    PowerPoint.Slide magnifyingSlide = AddMagnifyingSlideWithBackground(currentSlide, selectedShape);
                    PowerPoint.Slide magnifiedSlide = AddMagnifiedSlideWithBackground(magnifyingSlide, selectedShape);
                    PowerPoint.Slide demagnifyingSlide = AddDeMagnifyingSlideWithBackground(magnifyingSlide, selectedShape);

                    Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(magnifyingSlide.SlideIndex);
                    Globals.ThisAddIn.Application.CommandBars.ExecuteMso("AnimationPreview");
                }
                //selectedShape.Visible = Office.MsoTriState.msoFalse;
                selectedShape.Delete();
                AddAckSlide();
            }
            catch (Exception e)
            {
                LogException(e, "ZoomBtnClick");
                throw;
            }
        }
        public void ReloadSpotlightButtonClick(Office.IRibbonControl control)
        {
            try
            {
                Spotlight.ReloadSpotlightEffect();
            }
            catch (Exception e)
            {
                LogException(e, "ReloadSpotlightButtonClick");
                throw;
            }
        }
        public void SpotlightBtnClick(Office.IRibbonControl control)
        {
            try
            {
                if (Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange.Count == 0)
                    return;
                Spotlight.AddSpotlightEffect();
            }
            catch (Exception e)
            {
                LogException(e, "SpotlightBtnClick");
                throw;
            }
        }
        public void AboutButtonClick(Office.IRibbonControl control)
        {
            //AboutForm form = new AboutForm();
            //form.Show();
            System.Windows.Forms.MessageBox.Show("          PowerPointLabs Plugin Version 1.5 [Release date: 27 Feb 2014]\n     Developed at School of Computing, National University of Singapore.\n        For more information, visit our website http://PowerPointLabs.info", "About PowerPointLabs");
        }
        public void HelpButtonClick(Office.IRibbonControl control)
        {
            try
            {
                string myURL = "http://powerpointlabs.info/docs.html";
                System.Diagnostics.Process.Start(myURL);
            }
            catch (Exception e)
            {
                LogException(e, "HelpButtonClick");
                throw;
            }
        }
        public void FeedbackButtonClick(Office.IRibbonControl control)
        {
            try
            {
                string myURL = "http://powerpointlabs.info/contact.html";
                System.Diagnostics.Process.Start(myURL);
            }
            catch (Exception e)
            {
                LogException(e, "FeedbackButtonClick");
                throw;
            }
        }
        public void HighlightBulletsButtonClick(Office.IRibbonControl control)
        {
            System.Windows.Forms.MessageBox.Show("This feature is coming soon!                ", "Coming Soon");
        }
        private PowerPoint.Shape FindMatchingShape(PowerPoint.Slide slideToSearch, PowerPoint.Shape shapeToSearch)
        {
            PowerPoint.Shape shapeToReturn = null;
            foreach (PowerPoint.Shape sh in slideToSearch.Shapes)
            {
                if (sh.Name.Equals(shapeToSearch.Name))
                {
                    shapeToReturn = sh;
                    break;
                }
            }
            return shapeToReturn;
        }
        private void ZoomInOmitBackground(PowerPoint.Slide currentSlide, PowerPoint.Shape shape)
        {
            PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
            PowerPoint.Slide nextSlide = GetNextSlide(currentSlide);
            PowerPoint.Slide tempSlide = nextSlide;
            if (nextSlide.Name.Contains("PPTLabsZoomIn") && nextSlide.SlideIndex < presentation.Slides.Count)
            {
                nextSlide = GetNextSlide(tempSlide);
                tempSlide.Delete();
            }

            if (nextSlide.Shapes.Count == 0)
                return;

            Globals.ThisAddIn.Application.ActiveWindow.Selection.Unselect();
            Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(nextSlide.SlideIndex);
            String tempFileName = "";
            List<PowerPoint.Shape> nextSlideShapes = new List<PowerPoint.Shape>();
            foreach (PowerPoint.Shape sh in nextSlide.Shapes)
            {
                if(!HasEntryAnimation(nextSlide, sh))
                    nextSlideShapes.Add(sh);
            }

            foreach (PowerPoint.Shape sh in nextSlideShapes)
            {
                sh.Copy();
                PowerPoint.Shape tempPicture1 = nextSlide.Shapes.Paste()[1];
                tempPicture1.LockAspectRatio = Office.MsoTriState.msoFalse;
                tempPicture1.Left = sh.Left;
                tempPicture1.Top = sh.Top;
                tempPicture1.Width = sh.Width;
                tempPicture1.Height = sh.Height;
                tempPicture1.Select(Office.MsoTriState.msoFalse);
            }
            PowerPoint.Selection sel = Globals.ThisAddIn.Application.ActiveWindow.Selection;
            PowerPoint.Shape shapeGroup = null;
            if (sel.ShapeRange.Count > 1)
            {
                shapeGroup = sel.ShapeRange.Group();
            }
            else
            {
                shapeGroup = sel.ShapeRange[1];
            }

            shapeGroup.Name = "PPTZoomInShape" + GetTimestamp(DateTime.Now);
            shapeGroup.Copy();
            PowerPoint.Shape shapePictureNextSlide = nextSlide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
            shapePictureNextSlide.Left = shapeGroup.Left + (shapeGroup.Width / 2) - (shapePictureNextSlide.Width / 2);
            shapePictureNextSlide.Top = shapeGroup.Top + (shapeGroup.Height / 2) - (shapePictureNextSlide.Height / 2);
            shapePictureNextSlide.Name = shapeGroup.Name;
            shapePictureNextSlide.Copy();

            PowerPoint.Shape shapePictureCurrentSlide = currentSlide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
            if (shape.Name.Contains("PPTZoomInShape"))
            {
                shapePictureCurrentSlide.LockAspectRatio = Office.MsoTriState.msoFalse;
                shapePictureCurrentSlide.Left = shape.Left;
                shapePictureCurrentSlide.Top = shape.Top;
                shapePictureCurrentSlide.Width = shape.Width;
                shapePictureCurrentSlide.Height = shape.Height;
            }
            else
            {
                shapePictureCurrentSlide.LockAspectRatio = Office.MsoTriState.msoTrue;
                if (shape.Width > shape.Height)
                {
                    shapePictureCurrentSlide.Height = shape.Height;
                }
                else
                {
                    shapePictureCurrentSlide.Width = shape.Width;
                }
                shapePictureCurrentSlide.Left = shape.Left + (shape.Width / 2) - (shapePictureCurrentSlide.Width / 2);
                shapePictureCurrentSlide.Top = shape.Top + (shape.Height / 2) - (shapePictureCurrentSlide.Height / 2);
            }

            shapePictureCurrentSlide.Name = shapeGroup.Name;
            shape.Visible = Office.MsoTriState.msoFalse;

            PowerPoint.Slide addedSlide = currentSlide.Duplicate()[1];
            addedSlide.Name = "PPTLabsZoomIn" + GetTimestamp(DateTime.Now);
            PowerPoint.Shape shapePicture = FindIdenticalShape(addedSlide, shapePictureCurrentSlide); 
            
            PowerPoint.Sequence sequence = currentSlide.TimeLine.MainSequence;
            PowerPoint.Effect effectAppear = sequence.AddEffect(shapePictureCurrentSlide, PowerPoint.MsoAnimEffect.msoAnimEffectFade, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerOnPageClick);
            effectAppear.Timing.Duration = 0.50f;
            sequence = addedSlide.TimeLine.MainSequence;

            PowerPoint.Effect effectMotion = null;
            PowerPoint.Effect effectResize = null;
            PowerPoint.Effect effectDisappear = null;
            PowerPoint.Effect effectFade = null;

            tempFileName = Path.GetTempFileName();
            Properties.Resources.Indicator.Save(tempFileName);
            PowerPoint.Shape indicatorShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
            indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
            indicatorShape.Top = 0;
            indicatorShape.Width = 120;
            indicatorShape.Height = 84;
            indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
            effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            effectDisappear.Exit = Office.MsoTriState.msoTrue;
            effectDisappear.Timing.Duration = 0;
            bool fadeFlag = false;

            foreach (PowerPoint.Shape sh in addedSlide.Shapes)
            {
                if (!sh.Equals(indicatorShape) && !sh.Equals(shapePicture))
                {
                    effectFade = sequence.AddEffect(sh, PowerPoint.MsoAnimEffect.msoAnimEffectFade, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                    effectFade.Exit = Office.MsoTriState.msoTrue;
                    effectFade.Timing.Duration = 0.5f;
                    fadeFlag = true;
                }
            }

            PowerPoint.MsoAnimTriggerType trigger = (fadeFlag) ? PowerPoint.MsoAnimTriggerType.msoAnimTriggerAfterPrevious : PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious;
            float finalWidth = shapePictureNextSlide.Width;
            float initialWidth = shapePicture.Width;
            float finalHeight = shapePictureNextSlide.Height;
            float initialHeight = shapePicture.Height;

            float finalX = (shapePictureNextSlide.Left + (shapePictureNextSlide.Width) / 2);
            float initialX = (shapePicture.Left + (shapePicture.Width) / 2);
            float finalY = (shapePictureNextSlide.Top + (shapePictureNextSlide.Height) / 2);
            float initialY = (shapePicture.Top + (shapePicture.Height) / 2);

            effectMotion = sequence.AddEffect(shapePicture, PowerPoint.MsoAnimEffect.msoAnimEffectPathDown, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, trigger);
            PowerPoint.AnimationBehavior motion = effectMotion.Behaviors[1];
            effectMotion.Timing.Duration = defaultDuration;
            trigger = PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious;
            float point1X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
            float point1Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
            float point2X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
            float point2Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
            float point3X = (finalX - initialX) / presentation.PageSetup.SlideWidth;
            float point3Y = (finalY - initialY) / presentation.PageSetup.SlideHeight;

            motion.MotionEffect.Path = "M 0 0 C " + point1X + " " + point1Y + " " + point2X + " " + point2Y + " " + point3X + " " + point3Y + " E";
            effectMotion.Timing.SmoothStart = Office.MsoTriState.msoFalse;
            effectMotion.Timing.SmoothEnd = Office.MsoTriState.msoFalse;

            effectResize = sequence.AddEffect(shapePicture, PowerPoint.MsoAnimEffect.msoAnimEffectGrowShrink, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, trigger);
            PowerPoint.AnimationBehavior resize = effectResize.Behaviors[1];
            effectResize.Timing.Duration = defaultDuration;
            resize.ScaleEffect.ByX = (finalWidth / initialWidth) * 100;
            resize.ScaleEffect.ByY = (finalHeight / initialHeight) * 100;
            trigger = PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious;

            shapeGroup.Delete();
            shapePictureNextSlide.Delete();            

            indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
            addedSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoTrue;
            addedSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoFalse;
            addedSlide.SlideShowTransition.AdvanceTime = 0;
            addedSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
            if (addedSlide.HasNotesPage == Office.MsoTriState.msoTrue)
            {
                foreach (PowerPoint.Shape sh in addedSlide.NotesPage.Shapes)
                {
                    if (sh.TextFrame.HasText == Office.MsoTriState.msoTrue)
                        sh.TextEffect.Text = "";
                }
            }

            foreach (PowerPoint.Shape sh in addedSlide.Shapes)
            {
                if (sh.Type == Office.MsoShapeType.msoMedia)
                    sh.Delete();
            }
            nextSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectFadeSmoothly;
            nextSlide.SlideShowTransition.Duration = 0.25f;
            Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(addedSlide.SlideIndex);
            Globals.ThisAddIn.Application.CommandBars.ExecuteMso("AnimationPreview");
            AddAckSlide();
        }
        public void AddZoomInButtonClick(Office.IRibbonControl control)
        {
            //System.Windows.Forms.MessageBox.Show("This feature is coming soon!                ", "Coming Soon");
            
            //Get References of current and next slides
            PowerPoint.Slide currentSlide = GetCurrentSlide();
            if (Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange.Count == 0)
                return;

            PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
            PowerPoint.Shape shape = Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange[1];
            DeleteShapeAnnimations(currentSlide, shape);
            if (shape.HasTextFrame == Office.MsoTriState.msoTrue)
            {
                if (shape.TextFrame.HasText == Office.MsoTriState.msoTrue)
                {
                    shape.TextFrame.TextRange.Text = "";
                }
            }
            shape.Rotation = 0;

            if (currentSlide != null && currentSlide.SlideIndex != presentation.Slides.Count)
            {
                if (!backgroundZoomChecked)
                {
                    ZoomInOmitBackground(currentSlide, shape);
                }
                else
                {
                    PowerPoint.Slide nextSlide = GetNextSlide(currentSlide);
                    PowerPoint.Slide tempSlide = nextSlide;
                    if (nextSlide.Name.Contains("PPTLabsZoomIn") && nextSlide.SlideIndex < presentation.Slides.Count)
                    {
                        nextSlide = GetNextSlide(tempSlide);
                        tempSlide.Delete();
                    }

                    //String tempFileName = Path.GetTempFileName();
                    //nextSlide.Export(tempFileName, "PNG");
                    tempSlide = nextSlide.Duplicate()[1];
                    foreach (PowerPoint.Shape sh in nextSlide.Shapes)
                    {
                        PowerPoint.Shape tmp = FindIdenticalShape(tempSlide, sh);
                        if (HasEntryAnimation(tempSlide, tmp))
                            tmp.Delete();
                    }
                    tempSlide.Copy();
                    PowerPoint.Shape shapePictureCurrentSlide = currentSlide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
                    tempSlide.Delete();

                    if (shape.Name.Contains("PPTZoomInShape"))
                    {
                        shapePictureCurrentSlide.LockAspectRatio = Office.MsoTriState.msoFalse;
                        shapePictureCurrentSlide.Left = shape.Left;
                        shapePictureCurrentSlide.Top = shape.Top;
                        shapePictureCurrentSlide.Width = shape.Width;
                        shapePictureCurrentSlide.Height = shape.Height;
                    }
                    else
                    {
                        shapePictureCurrentSlide.LockAspectRatio = Office.MsoTriState.msoTrue;
                        if (shape.Width > shape.Height)
                        {
                            shapePictureCurrentSlide.Height = shape.Height;
                        }
                        else
                        {
                            shapePictureCurrentSlide.Width = shape.Width;
                        }
                        shapePictureCurrentSlide.Left = shape.Left + (shape.Width / 2) - (shapePictureCurrentSlide.Width / 2);
                        shapePictureCurrentSlide.Top = shape.Top + (shape.Height / 2) - (shapePictureCurrentSlide.Height / 2);
                    }
                    //shape.Fill.UserPicture(tempFileName);
                    //shape.Line.Visible = Office.MsoTriState.msoFalse;
                    shape.Visible = Office.MsoTriState.msoFalse;
                    shapePictureCurrentSlide.Name = "PPTZoomInShape" + GetTimestamp(DateTime.Now);

                    PowerPoint.Sequence sequence = currentSlide.TimeLine.MainSequence;
                    PowerPoint.Effect effectAppear = sequence.AddEffect(shapePictureCurrentSlide, PowerPoint.MsoAnimEffect.msoAnimEffectFade, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerOnPageClick);
                    effectAppear.Timing.Duration = 0.50f;

                    PowerPoint.Slide addedSlide = Globals.ThisAddIn.Application.ActivePresentation.Slides.AddSlide(currentSlide.SlideIndex + 1, currentSlide.CustomLayout);
                    addedSlide.Name = "PPTLabsZoomIn" + GetTimestamp(DateTime.Now);

                    currentSlide.Copy();
                    //tempFileName = Path.GetTempFileName() + ".png";
                    //currentSlide.Export(tempFileName, "PNG");
                    //PowerPoint.Shape zoomShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, 0, 0);
                    PowerPoint.Shape zoomShape = addedSlide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
                    sequence = addedSlide.TimeLine.MainSequence;

                    zoomShape.LockAspectRatio = Office.MsoTriState.msoFalse;
                    zoomShape.Left = 0;
                    zoomShape.Top = 0;
                    zoomShape.Width = presentation.PageSetup.SlideWidth;
                    zoomShape.Height = presentation.PageSetup.SlideHeight;
                    zoomShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
                    PowerPoint.Effect effectMotion = null;
                    PowerPoint.Effect effectResize = null;
                    //PowerPoint.Effect effectRotate = null;
                    PowerPoint.Effect effectDisappear = null;

                    String tempFileName = Path.GetTempFileName();
                    Properties.Resources.Indicator.Save(tempFileName);
                    PowerPoint.Shape indicatorShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
                    indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
                    indicatorShape.Top = 0;
                    indicatorShape.Width = 120;
                    indicatorShape.Height = 84;
                    indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
                    effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                    effectDisappear.Exit = Office.MsoTriState.msoTrue;
                    effectDisappear.Timing.Duration = 0;

                    //float angle = GetMinimumRotation(shape.Rotation, 0) * (float)Math.PI / 180.0f;

                    float finalWidth = presentation.PageSetup.SlideWidth;
                    float initialWidth = shapePictureCurrentSlide.Width;
                    float finalHeight = presentation.PageSetup.SlideHeight;
                    float initialHeight = shapePictureCurrentSlide.Height;

                    float finalX = (presentation.PageSetup.SlideWidth / 2) * (finalWidth / initialWidth);
                    float initialX = (shapePictureCurrentSlide.Left + (shapePictureCurrentSlide.Width) / 2) * (finalWidth / initialWidth);
                    float finalY = (presentation.PageSetup.SlideHeight / 2) * (finalHeight / initialHeight);
                    float initialY = (shapePictureCurrentSlide.Top + (shapePictureCurrentSlide.Height) / 2) * (finalHeight / initialHeight);

                    //float s = (float)Math.Sin(angle);
                    //float c = (float)Math.Cos(angle);

                    //finalX -= initialX;
                    //finalY -= initialY;

                    //// rotate point
                    //float newX = (finalX * c - finalY * s);
                    //float newY = (finalX * s + finalY * c);

                    //finalX = newX + initialX;
                    //finalY = newY + initialY;

                    effectMotion = sequence.AddEffect(zoomShape, PowerPoint.MsoAnimEffect.msoAnimEffectPathDown, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                    PowerPoint.AnimationBehavior motion = effectMotion.Behaviors[1];
                    effectMotion.Timing.Duration = defaultDuration;
                    float point1X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
                    float point1Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
                    float point2X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
                    float point2Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
                    float point3X = (finalX - initialX) / presentation.PageSetup.SlideWidth;
                    float point3Y = (finalY - initialY) / presentation.PageSetup.SlideHeight;

                    motion.MotionEffect.Path = "M 0 0 C " + point1X + " " + point1Y + " " + point2X + " " + point2Y + " " + point3X + " " + point3Y + " E";
                    effectMotion.Timing.SmoothStart = Office.MsoTriState.msoFalse;
                    effectMotion.Timing.SmoothEnd = Office.MsoTriState.msoFalse;

                    effectResize = sequence.AddEffect(zoomShape, PowerPoint.MsoAnimEffect.msoAnimEffectGrowShrink, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                    PowerPoint.AnimationBehavior resize = effectResize.Behaviors[1];
                    effectResize.Timing.Duration = defaultDuration;
                    resize.ScaleEffect.ByX = (finalWidth / initialWidth) * 100;
                    resize.ScaleEffect.ByY = (finalHeight / initialHeight) * 100;

                    //effectRotate = sequence.AddEffect(zoomShape, PowerPoint.MsoAnimEffect.msoAnimEffectSpin, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                    //PowerPoint.AnimationBehavior rotate = effectRotate.Behaviors[1];
                    //effectRotate.Timing.Duration = defaultDuration;
                    //effectRotate.EffectParameters.Amount = GetMinimumRotation(shape.Rotation, 0);

                    indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
                    addedSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoTrue;
                    addedSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoFalse;
                    addedSlide.SlideShowTransition.AdvanceTime = 0;
                    addedSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
                    if (addedSlide.HasNotesPage == Office.MsoTriState.msoTrue)
                    {
                        foreach (PowerPoint.Shape sh in addedSlide.NotesPage.Shapes)
                        {
                            if (sh.TextFrame.HasText == Office.MsoTriState.msoTrue)
                                sh.TextEffect.Text = "";
                        }
                    }

                    foreach (PowerPoint.Shape sh in addedSlide.Shapes)
                    {
                        if (sh.Type == Office.MsoShapeType.msoMedia)
                            sh.Delete();
                    }

                    //if (nextSlide.SlideShowTransition.EntryEffect != PowerPoint.PpEntryEffect.ppEffectFade && nextSlide.SlideShowTransition.EntryEffect != PowerPoint.PpEntryEffect.ppEffectFadeSmoothly)
                    //    nextSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
                    nextSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectFadeSmoothly;
                    nextSlide.SlideShowTransition.Duration = 0.25f;
                    Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(addedSlide.SlideIndex);
                    Globals.ThisAddIn.Application.CommandBars.ExecuteMso("AnimationPreview");
                    AddAckSlide();
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Please select the correct slide", "Unable to Add Animations");
            }
        }
        private PowerPoint.Shape convertShapeToPicture(PowerPoint.Slide slide, PowerPoint.Shape sh)
        {
            sh.Copy();
            PowerPoint.Shape tempPicture1 = slide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
            tempPicture1.LockAspectRatio = Office.MsoTriState.msoFalse;
            tempPicture1.Left = sh.Left;
            tempPicture1.Top = sh.Top;
            tempPicture1.Width = sh.Width;
            tempPicture1.Height = sh.Height;
            tempPicture1.Name = sh.Name;
            //sh.Delete();
            return tempPicture1;

        }
        private void ZoomOutOmitBackground(PowerPoint.Slide currentSlide, PowerPoint.Shape shape)
        {
            PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
            PowerPoint.Slide prevSlide = GetPrevSlide(currentSlide);
            PowerPoint.Slide tempSlide = prevSlide;
            while (prevSlide.Name.Contains("PPTLabsZoomOut") && prevSlide.SlideIndex > 1)
            {
                prevSlide = GetPrevSlide(tempSlide);
                tempSlide.Delete();
            }

            if (prevSlide.Shapes.Count == 0)
                return;

            PowerPoint.Slide addedSlide = Globals.ThisAddIn.Application.ActivePresentation.Slides.AddSlide(prevSlide.SlideIndex + 1, prevSlide.CustomLayout);
            addedSlide.Name = "PPTLabsZoomOut" + GetTimestamp(DateTime.Now);

            Globals.ThisAddIn.Application.ActiveWindow.Selection.Unselect();
            Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(addedSlide.SlideIndex);
            String tempFileName = "";
            foreach (PowerPoint.Shape sh in prevSlide.Shapes)
            {
                sh.Copy();
                PowerPoint.Shape tempPicture1 = addedSlide.Shapes.Paste()[1];
                tempPicture1.LockAspectRatio = Office.MsoTriState.msoFalse;
                tempPicture1.Left = sh.Left;
                tempPicture1.Top = sh.Top;
                tempPicture1.Width = sh.Width;
                tempPicture1.Height = sh.Height;
                tempPicture1.Select(Office.MsoTriState.msoFalse);
            }
            PowerPoint.Selection sel = Globals.ThisAddIn.Application.ActiveWindow.Selection;
            PowerPoint.Shape shapeGroup = null;
            if (sel.ShapeRange.Count > 1)
            {
                shapeGroup = sel.ShapeRange.Group();
            }
            else
            {
                shapeGroup = sel.ShapeRange[1];
            }

            shapeGroup.Name = "PPTZoomOutShape" + GetTimestamp(DateTime.Now);
            shapeGroup.Copy();
            PowerPoint.Shape shapePictureAddedSlide = addedSlide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
            shapePictureAddedSlide.Left = shapeGroup.Left + (shapeGroup.Width / 2) - (shapePictureAddedSlide.Width / 2);
            shapePictureAddedSlide.Top = shapeGroup.Top + (shapeGroup.Height / 2) - (shapePictureAddedSlide.Height / 2);
            shapePictureAddedSlide.Name = shapeGroup.Name;
            shapePictureAddedSlide.Copy();

            PowerPoint.Shape shapePictureCurrentSlide = currentSlide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
            if (shape.Name.Contains("PPTZoomOutShape"))
            {
                shapePictureCurrentSlide.LockAspectRatio = Office.MsoTriState.msoFalse;
                shapePictureCurrentSlide.Left = shape.Left;
                shapePictureCurrentSlide.Top = shape.Top;
                shapePictureCurrentSlide.Width = shape.Width;
                shapePictureCurrentSlide.Height = shape.Height;
            }
            else
            {
                shapePictureCurrentSlide.LockAspectRatio = Office.MsoTriState.msoTrue;
                if (shape.Width > shape.Height)
                {
                    shapePictureCurrentSlide.Height = shape.Height;
                }
                else
                {
                    shapePictureCurrentSlide.Width = shape.Width;
                }
                shapePictureCurrentSlide.Left = shape.Left + (shape.Width / 2) - (shapePictureCurrentSlide.Width / 2);
                shapePictureCurrentSlide.Top = shape.Top + (shape.Height / 2) - (shapePictureCurrentSlide.Height / 2);
            }

            shapePictureCurrentSlide.Name = shapeGroup.Name;
            shape.Visible = Office.MsoTriState.msoFalse;

            PowerPoint.Sequence sequence = addedSlide.TimeLine.MainSequence;
            PowerPoint.Effect effectDisappear = null;
            PowerPoint.Effect effectMotion = null;
            PowerPoint.Effect effectResize = null;

            tempFileName = Path.GetTempFileName();
            Properties.Resources.Indicator.Save(tempFileName);
            PowerPoint.Shape indicatorShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
            indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
            indicatorShape.Top = 0;
            indicatorShape.Width = 120;
            indicatorShape.Height = 84;
            indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
            effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            effectDisappear.Exit = Office.MsoTriState.msoTrue;
            effectDisappear.Timing.Duration = 0;

            float initialX = (shapePictureAddedSlide.Left + (shapePictureAddedSlide.Width) / 2);
            float finalX = (shapePictureCurrentSlide.Left + (shapePictureCurrentSlide.Width) / 2);
            float initialY = (shapePictureAddedSlide.Top + (shapePictureAddedSlide.Height) / 2);
            float finalY = (shapePictureCurrentSlide.Top + (shapePictureCurrentSlide.Height) / 2);

            float initialWidth = shapePictureAddedSlide.Width;
            float finalWidth = shapePictureCurrentSlide.Width;
            float initialHeight = shapePictureAddedSlide.Height;
            float finalHeight = shapePictureCurrentSlide.Height;

            effectMotion = sequence.AddEffect(shapePictureAddedSlide, PowerPoint.MsoAnimEffect.msoAnimEffectPathDown, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            PowerPoint.AnimationBehavior motion = effectMotion.Behaviors[1];
            effectMotion.Timing.Duration = defaultDuration;
            float point1X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
            float point1Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
            float point2X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
            float point2Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
            float point3X = (finalX - initialX) / presentation.PageSetup.SlideWidth;
            float point3Y = (finalY - initialY) / presentation.PageSetup.SlideHeight;

            motion.MotionEffect.Path = "M 0 0 C " + point1X + " " + point1Y + " " + point2X + " " + point2Y + " " + point3X + " " + point3Y + " E";
            effectMotion.Timing.SmoothStart = Office.MsoTriState.msoFalse;
            effectMotion.Timing.SmoothEnd = Office.MsoTriState.msoFalse;

            effectResize = sequence.AddEffect(shapePictureAddedSlide, PowerPoint.MsoAnimEffect.msoAnimEffectGrowShrink, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
            PowerPoint.AnimationBehavior resize = effectResize.Behaviors[1];
            effectResize.Timing.Duration = defaultDuration;
            resize.ScaleEffect.ByX = (finalWidth / initialWidth) * 100;
            resize.ScaleEffect.ByY = (finalHeight / initialHeight) * 100;

            shapeGroup.Delete();
            indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
            addedSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoTrue;
            addedSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoFalse;
            addedSlide.SlideShowTransition.AdvanceTime = 0;
            addedSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;

            if (addedSlide.HasNotesPage == Office.MsoTriState.msoTrue)
            {
                foreach (PowerPoint.Shape sh in addedSlide.NotesPage.Shapes)
                {
                    if (sh.TextFrame.HasText == Office.MsoTriState.msoTrue)
                        sh.TextEffect.Text = "";
                }
            }

            foreach (PowerPoint.Shape sh in addedSlide.Shapes)
            {
                if (sh.Type == Office.MsoShapeType.msoMedia)
                    sh.Delete();
            }
            //if (currentSlide.SlideShowTransition.EntryEffect != PowerPoint.PpEntryEffect.ppEffectFade && currentSlide.SlideShowTransition.EntryEffect != PowerPoint.PpEntryEffect.ppEffectFadeSmoothly)
            //    currentSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
            currentSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectFadeSmoothly;
            currentSlide.SlideShowTransition.Duration = 0.25f;
            Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(addedSlide.SlideIndex);
            Globals.ThisAddIn.Application.CommandBars.ExecuteMso("AnimationPreview");
            AddAckSlide();
        }
        public void AddZoomOutButtonClick(Office.IRibbonControl control)
        {
            //System.Windows.Forms.MessageBox.Show("This feature is coming soon!                ", "Coming Soon");

            //Get References of current and next slides
            PowerPoint.Slide currentSlide = GetCurrentSlide();
            if (Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange.Count == 0)
                return;

            PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
            PowerPoint.Shape shape = Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange[1];
            DeleteShapeAnnimations(currentSlide, shape);
            if (shape.HasTextFrame == Office.MsoTriState.msoTrue)
            {
                if (shape.TextFrame.HasText == Office.MsoTriState.msoTrue)
                {
                    shape.TextFrame.TextRange.Text = "";
                }
            }
            shape.Rotation = 0;

            if (currentSlide != null && currentSlide.SlideIndex != 1)
            {
                if (!backgroundZoomChecked)
                {
                    ZoomOutOmitBackground(currentSlide, shape);
                }
                else
                {
                    PowerPoint.Slide prevSlide = GetPrevSlide(currentSlide);
                    PowerPoint.Slide tempSlide = prevSlide;
                    while (prevSlide.Name.Contains("PPTLabsZoomOut") && prevSlide.SlideIndex > 1)
                    {
                        prevSlide = GetPrevSlide(tempSlide);
                        tempSlide.Delete();
                    }

                    prevSlide.Copy();
                    PowerPoint.Shape shapePictureCurrentSlide = currentSlide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
                    if (shape.Name.Contains("PPTZoomOutShape"))
                    {
                        shapePictureCurrentSlide.LockAspectRatio = Office.MsoTriState.msoFalse;
                        shapePictureCurrentSlide.Left = shape.Left;
                        shapePictureCurrentSlide.Top = shape.Top;
                        shapePictureCurrentSlide.Width = shape.Width;
                        shapePictureCurrentSlide.Height = shape.Height;
                    }
                    else
                    {
                        shapePictureCurrentSlide.LockAspectRatio = Office.MsoTriState.msoTrue;
                        if (shape.Width > shape.Height)
                        {
                            shapePictureCurrentSlide.Height = shape.Height;
                        }
                        else
                        {
                            shapePictureCurrentSlide.Width = shape.Width;
                        }
                        shapePictureCurrentSlide.Left = shape.Left + (shape.Width / 2) - (shapePictureCurrentSlide.Width / 2);
                        shapePictureCurrentSlide.Top = shape.Top + (shape.Height / 2) - (shapePictureCurrentSlide.Height / 2);
                    }

                    //String tempFileName = Path.GetTempFileName();
                    //prevSlide.Export(tempFileName, "PNG");
                    //shape.Fill.UserPicture(tempFileName);
                    //shape.Line.Visible = Office.MsoTriState.msoFalse;
                    shape.Visible = Office.MsoTriState.msoFalse;
                    shapePictureCurrentSlide.Name = "PPTZoomOutShape" + GetTimestamp(DateTime.Now);

                    PowerPoint.Slide addedSlide = Globals.ThisAddIn.Application.ActivePresentation.Slides.AddSlide(currentSlide.SlideIndex + 1, currentSlide.CustomLayout);
                    addedSlide.Name = "PPTLabsZoomOut" + GetTimestamp(DateTime.Now);
                    addedSlide.MoveTo(currentSlide.SlideIndex);

                    currentSlide.Copy();
                    PowerPoint.Shape zoomShape = addedSlide.Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
                    zoomShape.LockAspectRatio = Office.MsoTriState.msoFalse;
                    zoomShape.Left = 0;
                    zoomShape.Top = 0;
                    zoomShape.Width = presentation.PageSetup.SlideWidth;
                    zoomShape.Height = presentation.PageSetup.SlideHeight;
                    shapePictureCurrentSlide.Copy();
                    PowerPoint.Shape zoomCopyShape = addedSlide.Shapes.Paste()[1];

                    Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(addedSlide.SlideIndex);
                    zoomCopyShape.Select();
                    zoomShape.Select(Office.MsoTriState.msoFalse);
                    PowerPoint.ShapeRange selection = Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange;
                    PowerPoint.Shape groupShape = selection.Group();

                    //PowerPoint.Sequence sequence = currentSlide.TimeLine.MainSequence;
                    //PowerPoint.Effect effectAppear = sequence.AddEffect(shape, PowerPoint.MsoAnimEffect.msoAnimEffectZoom, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerOnPageClick);
                    //effectAppear.Timing.Duration = 0.25f;

                    PowerPoint.Sequence sequence = addedSlide.TimeLine.MainSequence;
                    groupShape.LockAspectRatio = Office.MsoTriState.msoFalse;
                    groupShape.Left = (presentation.PageSetup.SlideWidth / 2) - ((shapePictureCurrentSlide.Left + (shape.Width) / 2) * presentation.PageSetup.SlideWidth / shapePictureCurrentSlide.Width);
                    groupShape.Top = (presentation.PageSetup.SlideHeight / 2) - ((shapePictureCurrentSlide.Top + (shapePictureCurrentSlide.Height) / 2) * presentation.PageSetup.SlideHeight / shapePictureCurrentSlide.Height);
                    groupShape.Width = presentation.PageSetup.SlideWidth * presentation.PageSetup.SlideWidth / shapePictureCurrentSlide.Width;
                    groupShape.Height = presentation.PageSetup.SlideHeight * presentation.PageSetup.SlideHeight / shapePictureCurrentSlide.Height;
                    //groupShape.Rotation = -1 * shape.Rotation;

                    groupShape.Left += (0 - zoomCopyShape.Left);
                    groupShape.Top += (0 - zoomCopyShape.Top);

                    groupShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
                    //groupShape.Ungroup();
                    //zoomCopyShape.Delete();
                    PowerPoint.Effect effectDisappear = null;

                    String tempFileName = Path.GetTempFileName();
                    Properties.Resources.Indicator.Save(tempFileName);
                    PowerPoint.Shape indicatorShape = addedSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
                    indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
                    indicatorShape.Top = 0;
                    indicatorShape.Width = 120;
                    indicatorShape.Height = 84;
                    indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
                    effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                    effectDisappear.Exit = Office.MsoTriState.msoTrue;
                    effectDisappear.Timing.Duration = 0;

                    float initialX = (groupShape.Left + (groupShape.Width) / 2);
                    float finalX = presentation.PageSetup.SlideWidth / 2;
                    float initialY = (groupShape.Top + (groupShape.Height) / 2);
                    float finalY = presentation.PageSetup.SlideHeight / 2;

                    float initialWidth = groupShape.Width;
                    float finalWidth = presentation.PageSetup.SlideWidth;
                    float initialHeight = groupShape.Height;
                    float finalHeight = presentation.PageSetup.SlideHeight;
                    float initialRotation = groupShape.Rotation;
                    //float finalRotation = 0;

                    int numFrames = 10;

                    float incrementWidth = ((finalWidth / initialWidth) - 1.0f) / numFrames;
                    float incrementHeight = ((finalHeight / initialHeight) - 1.0f) / numFrames;
                    //float incrementRotation = GetMinimumRotation(initialRotation, finalRotation) / numFrames;
                    float incrementLeft = (finalX - initialX) / numFrames;
                    float incrementTop = (finalY - initialY) / numFrames;

                    PowerPoint.Shape lastShape = groupShape;
                    for (int i = 1; i <= numFrames; i++)
                    {
                        PowerPoint.Shape dupShape = groupShape.Duplicate()[1];
                        if (i != 1)
                            sequence[sequence.Count].Delete();

                        dupShape.LockAspectRatio = Office.MsoTriState.msoFalse;
                        dupShape.Left = groupShape.Left;
                        dupShape.Top = groupShape.Top;
                        //dupShape.Rotation = groupShape.Rotation;

                        if (incrementWidth != 0.0f)
                        {
                            dupShape.ScaleWidth((1.0f + (incrementWidth * i)), Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromMiddle);
                        }

                        if (incrementHeight != 0.0f)
                        {
                            dupShape.ScaleHeight((1.0f + (incrementHeight * i)), Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromMiddle);
                        }

                        //if (incrementRotation != 0.0f)
                        //{
                        //    dupShape.Rotation += (incrementRotation * i);
                        //}

                        if (incrementLeft != 0.0f)
                        {
                            dupShape.Left += (incrementLeft * i);
                        }

                        if (incrementTop != 0.0f)
                        {
                            dupShape.Top += (incrementTop * i);
                        }

                        PowerPoint.Effect appear = sequence.AddEffect(dupShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                        //appear.Timing.Duration = 0.005f;
                        appear.Timing.TriggerDelayTime = ((defaultDuration / numFrames) * i);

                        PowerPoint.Effect disappear = sequence.AddEffect(lastShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                        disappear.Exit = Office.MsoTriState.msoTrue;
                        //disappear.Timing.Duration = 0.005f;
                        disappear.Timing.TriggerDelayTime = ((defaultDuration / numFrames) * i);

                        lastShape = dupShape;
                    }

                    indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
                    addedSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoTrue;
                    addedSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoFalse;
                    addedSlide.SlideShowTransition.AdvanceTime = 0;
                    addedSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;

                    if (addedSlide.HasNotesPage == Office.MsoTriState.msoTrue)
                    {
                        foreach (PowerPoint.Shape sh in addedSlide.NotesPage.Shapes)
                        {
                            if (sh.TextFrame.HasText == Office.MsoTriState.msoTrue)
                                sh.TextEffect.Text = "";
                        }
                    }

                    foreach (PowerPoint.Shape sh in addedSlide.Shapes)
                    {
                        if (sh.Type == Office.MsoShapeType.msoMedia)
                            sh.Delete();
                    }
                    //if (currentSlide.SlideShowTransition.EntryEffect != PowerPoint.PpEntryEffect.ppEffectFade && currentSlide.SlideShowTransition.EntryEffect != PowerPoint.PpEntryEffect.ppEffectFadeSmoothly)
                    //    currentSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
                    currentSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectFadeSmoothly;
                    currentSlide.SlideShowTransition.Duration = 0.25f;
                    Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(addedSlide.SlideIndex);
                    Globals.ThisAddIn.Application.CommandBars.ExecuteMso("AnimationPreview");
                    AddAckSlide();
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Please select the correct slide", "Unable to Add Animations");
            }
        }

        public void CropShapeButtonClick(Office.IRibbonControl control)
        {
            var selection = Globals.ThisAddIn.Application.
                ActiveWindow.Selection;
            CropShape(ref selection);
        }

        //Dropdown Callbacks
        //public int OnGetItemCount(Office.IRibbonControl control)
        //{
        //    return effectMapping.Count;
        //}

        //public String OnGetItemLabel(Office.IRibbonControl control, int index)
        //{
        //    String[] keys = effectMapping.Keys.ToArray();
        //    return keys[index];
        //}

        //public void OnSelectItem(Office.IRibbonControl control, String selectedId, int selectedIndex)
        //{
        //    String[] keys = effectMapping.Keys.ToArray();
        //    defaultEffect = effectMapping[keys[selectedIndex]];
        //}

        public System.Drawing.Bitmap GetAddAnimationImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.AddAnimation);
            }
            catch (Exception e)
            {
                LogException(e, "GetAddAnimationImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetReloadAnimationImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.ReloadAnimation);
            }
            catch (Exception e)
            {
                LogException(e, "GetReloadAnimationImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetSpotlightImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.Spotlight);
            }
            catch (Exception e)
            {
                LogException(e, "GetSpotlightImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetReloadSpotlightImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.ReloadSpotlight);
            }
            catch (Exception e)
            {
                LogException(e, "GetReloadSpotlightImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetHighlightBulletsImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.Bullets);
            }
            catch (Exception e)
            {
                LogException(e, "GetHighlightBulletsImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetZoomInImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.ZoomIn);
            }
            catch (Exception e)
            {
                LogException(e, "GetZoomInImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetZoomOutImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.ZoomOut);
            }
            catch (Exception e)
            {
                LogException(e, "GetZoomOutImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetZoomToAreaImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.ZoomToArea);
            }
            catch (Exception e)
            {
                LogException(e, "GetZoomToAreaImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetZoomToAreaContextImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.ZoomToAreaContext);
            }
            catch (Exception e)
            {
                LogException(e, "GetZoomToAreaContextImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetCropShapeImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.CutOutShape);
            }
            catch (Exception e)
            {
                LogException(e, "GetCropShapeImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetAboutImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.About);
            }
            catch (Exception e)
            {
                LogException(e, "GetAboutImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetHelpImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.Help);
            }
            catch (Exception e)
            {
                LogException(e, "GetHelpImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetFeedbackImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.Feedback);
            }
            catch (Exception e)
            {
                LogException(e, "GetFeedbackImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetAddAudioImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.AddAudio);
            }
            catch (Exception e)
            {
                LogException(e, "GetAddAudioImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetRemoveAudioImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.RemoveAudio);
            }
            catch (Exception e)
            {
                LogException(e, "GetRemoveAudioImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetAddCaptionsImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.AddCaption);
            }
            catch (Exception e)
            {
                LogException(e, "GetAddCaptionsImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetRemoveCaptionsImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.RemoveCaption);
            }
            catch (Exception e)
            {
                LogException(e, "GetRemoveCaptionsImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetAddAudioContextImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.AddNarrationContext);
            }
            catch (Exception e)
            {
                LogException(e, "GetAddAudioContextImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetPreviewNarrationContextImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.SpeakTextContext);
            }
            catch (Exception e)
            {
                LogException(e, "GetPreviewNarrationContextImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetInSlideAnimationImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.InSlideAnimation);
            }
            catch (Exception e)
            {
                LogException(e, "GetInSlideAnimationImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetAddAnimationContextImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.AddAnimationContext);
            }
            catch (Exception e)
            {
                LogException(e, "GetAddAnimationContextImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetReloadAnimationContextImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.ReloadAnimationContext);
            }
            catch (Exception e)
            {
                LogException(e, "GetReloadAnimationContextImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetAddSpotlightContextImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.AddSpotlightContext);
            }
            catch (Exception e)
            {
                LogException(e, "GetAddSpotlightContextImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetEditNameContextImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.EditNameContext);
            }
            catch (Exception e)
            {
                LogException(e, "GetEditNameContextImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetInSlideAnimationContextImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.InSlideContext);
            }
            catch (Exception e)
            {
                LogException(e, "GetInSlideAnimationContextImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetZoomInContextImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.ZoomInContext);
            }
            catch (Exception e)
            {
                LogException(e, "GetZoomInContextImage");
                throw;
            }
        }
        public System.Drawing.Bitmap GetZoomOutContextImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.ZoomOutContext);
            }
            catch (Exception e)
            {
                LogException(e, "GetZoomOutContextImage");
                throw;
            }
        }
        //Duration Callbacks
        public void OnChangeDuration(Office.IRibbonControl control, String text)
        {
            try
            {
                if (text == "")
                    defaultDuration = 0.01f;
                else
                {
                    float enteredValue = float.Parse(text);
                    if (enteredValue < 0.01)
                        defaultDuration = 0.01f;
                    else if (enteredValue > 59.0)
                        defaultDuration = 59.0f;
                    else
                        defaultDuration = enteredValue;
                }
                ribbon.InvalidateControl("animationDurationOption");
            }
            catch (Exception e)
            {
                LogException(e, "OnChangeDuration");
                throw;
            }
        }
        public String OnGetDurationText(Office.IRibbonControl control)
        {
            try
            {
                return defaultDuration.ToString();
            }
            catch (Exception e)
            {
                LogException(e, "OnGetDurationText");
                throw;
            }
        }

        //Checkbox Callbacks
        public void AnimationStyleChanged(Office.IRibbonControl control, bool pressed)
        {
            try
            {
                if (pressed)
                {
                    frameAnimationChecked = true;
                }
                else
                {
                    frameAnimationChecked = false;
                }
            }
            catch (Exception e)
            {
                LogException(e, "AnimationStyleChanged");
                throw;
            }
        }
        public bool AnimationStyleGetPressed(Office.IRibbonControl control)
        {
            try
            {
                return frameAnimationChecked;
            }
            catch (Exception e)
            {
                LogException(e, "AnimationStyleGetPressed");
                throw;
            }
        }
        public void ZoomStyleChanged(Office.IRibbonControl control, bool pressed)
        {
            try
            {
                if (pressed)
                {
                    backgroundZoomChecked = true;
                }
                else
                {
                    backgroundZoomChecked = false;
                }
            }
            catch (Exception e)
            {
                LogException(e, "ZoomStyleChanged");
                throw;
            }
        }
        public bool ZoomStyleGetPressed(Office.IRibbonControl control)
        {
            try
            {
                return backgroundZoomChecked;
            }
            catch (Exception e)
            {
                LogException(e, "ZoomStyleGetPressed");
                throw;
            }
        }
        //public void SpotlightDeleteOptionChanged(Office.IRibbonControl control, bool pressed)
        //{
        //    try
        //    {
        //        if (pressed)
        //        {
        //            spotlightDelete = true;
        //        }
        //        else
        //        {
        //            spotlightDelete = false;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        LogException(e, "SpotlightDeleteOptionChanged");
        //        throw;
        //    }
        //}
        //public bool SpotlightDeleteOptionGetPressed(Office.IRibbonControl control)
        //{
        //    try
        //    {
        //        return spotlightDelete;
        //    }
        //    catch (Exception e)
        //    {
        //        LogException(e, "SpotlightDeleteOptionGetPressed");
        //        throw;
        //    }
        //}


        //Transparency Callbacks
        public void OnChangeTransparency(Office.IRibbonControl control, String text)
        {
            try
            {
                if (text.Contains('%'))
                {
                    text = text.Substring(0, text.IndexOf('%'));
                }
                float result;
                if (float.TryParse(text, out result))
                {
                    if (result > 0 && result <= 100)
                    {
                        Spotlight.defaultTransparency = result;
                        Spotlight.defaultTransparency /= 100;
                    }
                }
                ribbon.InvalidateControl("spotlightTransparency");
            }
            catch (Exception e)
            {
                LogException(e, "OnChangeTransparency");
                throw;
            }
        }
        public String OnGetTransparency(Office.IRibbonControl control)
        {
            try
            {
                return (Spotlight.defaultTransparency * 100).ToString() + "%";
            }
            catch (Exception e)
            {
                LogException(e, "OnGetTransparency");
                throw;
            }
        }

        //Spotlight Edges Callbacks
        public int OnGetItemCountSpotlight(Office.IRibbonControl control)
        {
            try
            {
                return Spotlight.softEdgesMapping.Count;
            }
            catch (Exception e)
            {
                LogException(e, "OnGetItemCountSpotlight");
                throw;
            }
        }
        public String OnGetItemLabelSpotlight(Office.IRibbonControl control, int index)
        {
            try
            {
                String[] keys = Spotlight.softEdgesMapping.Keys.ToArray();
                return keys[index];
            }
            catch (Exception e)
            {
                LogException(e, "OnGetItemLabelSpotlight");
                throw;
            }
        }
        public void OnSelectItemSpotlight(Office.IRibbonControl control, String selectedId, int selectedIndex)
        {
            try
            {
                String[] keys = Spotlight.softEdgesMapping.Keys.ToArray();
                Spotlight.defaultSoftEdges = Spotlight.softEdgesMapping[keys[selectedIndex]];
            }
            catch (Exception e)
            {
                LogException(e, "OnSelectItemSpotlight");
                throw;
            }
        }
        public int OnGetSelectedItemIndexSpotlight(Office.IRibbonControl control)
        {
            try
            {
                float[] values = Spotlight.softEdgesMapping.Values.ToArray();
                return Array.IndexOf(values, Spotlight.defaultSoftEdges);
            }
            catch (Exception e)
            {
                LogException(e, "OnGetSelectedItemIndexSpotlight");
                throw;
            }
        }

        //Control Enabled Callbacks
        public bool OnGetEnabledSpotlight(Office.IRibbonControl control)
        {
            return spotlightEnabled;
        }
        public bool OnGetEnabledReloadSpotlight(Office.IRibbonControl control)
        {
            return reloadSpotlight;
        }
        public bool OnGetEnabledAddAutoMotion(Office.IRibbonControl control)
        {
            return addAutoMotionEnabled;
        }
        public bool OnGetEnabledReloadAutoMotion(Office.IRibbonControl control)
        {
            return reloadAutoMotionEnabled;
        }
        public bool OnGetEnabledAddInSlide(Office.IRibbonControl control)
        {
            return inSlideEnabled;
        }
        public bool OnGetEnabledZoomButton(Office.IRibbonControl control)
        {
            return zoomButtonEnabled;
        }

        //Edit Name Callbacks
        public void NameEditBtnClick(Office.IRibbonControl control)
        {
            try
            {
                PowerPoint.Shape selectedShape = (PowerPoint.Shape)Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange[1];
                Form1 editForm = new Form1(this, selectedShape.Name);
                editForm.Show();
            }
            catch (Exception e)
            {
                LogException(e, "NameEditBtnClick");
                throw;
            }
        }
        public void ShapeNameEdited(String newName)
        {
            try
            {
                PowerPoint.Shape selectedShape = (PowerPoint.Shape)Globals.ThisAddIn.Application.ActiveWindow.Selection.ShapeRange[1];
                selectedShape.Name = newName;
            }
            catch (Exception e)
            {
                LogException(e, "ShapeNameEdited");
                throw;
            }
        }

        #region NotesToAudio Button Callbacks
        public void SpeakSelectedTextClick(Office.IRibbonControl control)
        {
            NotesToAudio.SpeakSelectedText();
        }

        public void RemoveAudioClick(Office.IRibbonControl control)
        {
            if (_allSlides)
            {
                NotesToAudio.RemoveAudioFromAllSlides();
            }
            else
            {
                NotesToAudio.RemoveAudioFromCurrentSlide();
            }
        }

        public void AddAudioClick(Office.IRibbonControl control)
        {
            if (_allSlides)
            {
                NotesToAudio.EmbedAllSlideNotes();
            }
            else
            {
                NotesToAudio.EmbedCurrentSlideNotes();
            }

            PreviewAnimationsIfChecked();
        }

        public void ContextAddAudioClick(Office.IRibbonControl control)
        {
            NotesToAudio.EmbedCurrentSlideNotes();
            PreviewAnimationsIfChecked();
        }
        #endregion

        #region NotesToCaptions Button Callbacks

        public void AddCaptionClick(Office.IRibbonControl control)
        {
            if (_captionsAllSlides)
            {
                NotesToCaptions.EmbedCaptionsOnAllSlides();
            }
            else
            {
                NotesToCaptions.EmbedCaptionsOnCurrentSlide();
            }
        }

        public void RemoveCaptionClick(Office.IRibbonControl control)
        {
            if (_captionsAllSlides)
            {
                NotesToCaptions.RemoveCaptionsFromAllSlides();
            }
            else
            {
                NotesToCaptions.RemoveCaptionsFromCurrentSlide();
            }
        }

        public void ContextReplaceAudioClick(Office.IRibbonControl control)
        {
            NotesToAudio.ReplaceSelectedAudio();
        }

        #endregion

        #region NotesToAudio/Caption Helpers

        private void PreviewAnimationsIfChecked()
        {
            if (_previewCurrentSlide)
            {
                NotesToAudio.PreviewAnimations();
            }
        }

        public void AllSlidesChecked(Office.IRibbonControl control, bool pressed)
        {
            _allSlides = pressed;
        }

        public void PreviewCurrentSlideChecked(Office.IRibbonControl control, bool pressed)
        {
            _previewCurrentSlide = pressed;
        }

        public void AllSlidesCaptionsChecked(Office.IRibbonControl control, bool pressed)
        {
            _captionsAllSlides = pressed;
        }

        #region Dropdown Index/Label Handlers

        public int DefaultVoiceSelectedIndex(Office.IRibbonControl control)
        {
            return _voiceSelected;
        }

        public int DefaultVoicePickerCount(Office.IRibbonControl control)
        {
            return _voiceNames.Count();
        }

        public string DefaultVoicePickerLabel(Office.IRibbonControl control, int index)
        {
            return _voiceNames.ToArray()[index];
        }
        #endregion

        #region Dropdown Selection Handlers
        public void DefaultVoiceSelectionChanged(Office.IRibbonControl control, string selectedId, int selectedIndex)
        {
            _voiceSelected = selectedIndex;
            SetCoreVoicesToSelections();
        }

        #endregion

        private void SetCoreVoicesToSelections()
        {
            string defaultVoice = GetSelectedVoiceOrNull();
            NotesToAudio.SetDefaultVoice(defaultVoice);
        }

        private string GetSelectedVoiceOrNull()
        {
            string selectedVoice = null;
            try
            {
                selectedVoice = _voiceNames.ToArray()[_voiceSelected];
            }
            catch (IndexOutOfRangeException)
            {
                // No voices are installed.
                // (It should be impossible for the index to be out of range otherwise.)
            }
            return selectedVoice;
        }

        #endregion

        #endregion

        #region Fit To Slide | Fit To Width | Fit To Height

        private const int SelectedShapeIndex = 1;
        private const int TopMost = 0;
        private const int LeftMost = 0;

        public void FitToWidthClick(Office.IRibbonControl control)
        {
            DoFitToWidth();
        }

        public void FitToHeightClick(Office.IRibbonControl control)
        {
            DoFitToHeight();
        }

        private void DoFitToHeight()
        {
            var pageSetup = GetPageSetup();
            var selectedShape = GetSelectedShape();
            float shapeSizeRatio = GetSizeRatio(selectedShape.Height, selectedShape.Width);
            float factor = GetRotationFactorForFitToHeight(ref selectedShape);
            //fit to height
            selectedShape.Height = (pageSetup.SlideHeight / factor);
            selectedShape.Width = selectedShape.Height / shapeSizeRatio;
            //move to centre
            selectedShape.Left = (pageSetup.SlideWidth - selectedShape.Width) / 2;
            selectedShape.Top = TopMost;
            AdjustMoveToCentreForFitToHeight(ref selectedShape);
        }

        private void AdjustMoveToCentreForFitToHeight(ref PowerPoint.Shape shape)
        {
            float adjustLength;
            float rotation = SetupRotationValueForAdjustment(ref shape);
            //Pythagorean theorem
            float diagonal = (float)Math.Sqrt(Math.Pow(shape.Width / 2, 2)
                + Math.Pow(shape.Height / 2, 2));
            //Law of cosines
            float oppositeSideLength = (float)Math.Sqrt((Math.Pow(diagonal, 2) * 2
                * (1 - Math.Cos(rotation))));
            float angle1 = (float)Math.Atan(shape.Width / shape.Height);
            float angle2 = (float)((Math.PI - rotation) / 2);
            //case 1:
            if ((shape.Rotation >= 0 && shape.Rotation <= 90)
                || (shape.Rotation > 270 && shape.Rotation <= 360))
            {
                float targetAngle = (float)(Math.PI - angle1 - angle2);
                adjustLength = (float)(oppositeSideLength * Math.Cos(targetAngle));
            }
            //case 2:
            else/* case: 90 < rotation < 270)*/
            {
                float targetAngle = angle1 - angle2;
                adjustLength = (float)(oppositeSideLength * Math.Cos(targetAngle)) - shape.Height;
            }
            shape.Top += adjustLength;
        }

        private float GetRotationFactorForFitToHeight(ref PowerPoint.Shape shape)
        {
            //set up rotation value
            float rotation = SetupRotationValueForRotateFactor(ref shape);
            //calculate factor for Fit to Height
            float shapeSizeRatio = GetSizeRatio(shape.Height, shape.Width);
            float factor = (float)(Math.Cos(rotation) + Math.Sin(rotation) / shapeSizeRatio);
            return factor;
        }

        private void DoFitToWidth()
        {
            var pageSetup = GetPageSetup();
            var selectedShape = GetSelectedShape();
            float shapeSizeRatio = GetSizeRatio(selectedShape.Height, selectedShape.Width);
            //fit to width
            float factor = GetRotationFactorForFitToWidth(ref selectedShape);
            selectedShape.Height = pageSetup.SlideWidth / factor;
            selectedShape.Width = selectedShape.Height / shapeSizeRatio;
            //move to middle
            selectedShape.Top = (pageSetup.SlideHeight - selectedShape.Height) / 2;
            selectedShape.Left = LeftMost;
            //adjustment for rotation
            AdjustMoveToCentreForFitToWidth(ref selectedShape);
        }

        private void AdjustMoveToCentreForFitToWidth(ref PowerPoint.Shape shape)
        {
            float adjustLength;
            float rotation = SetupRotationValueForAdjustment(ref shape);
            //Pythagorean theorem
            float diagonal = (float)Math.Sqrt(Math.Pow(shape.Width / 2, 2)
                + Math.Pow(shape.Height / 2, 2));
            //Law of cosines
            float oppositeSideLength = (float)Math.Sqrt((Math.Pow(diagonal, 2) * 2
                * (1 - Math.Cos(rotation))));
            float angle1 = (float)Math.Atan(shape.Height / shape.Width);
            float angle2 = (float)((Math.PI - rotation) / 2);
            //case 1:
            if ((shape.Rotation >= 0 && shape.Rotation <= 90)
                || (shape.Rotation > 270 && shape.Rotation <= 360))
            {
                float targetAngle = (float)(Math.PI - angle1 - angle2);
                adjustLength = (float)(oppositeSideLength * Math.Cos(targetAngle));
            }
            //case 2:
            else/* case: 90 < rotation < 270)*/
            {
                float targetAngle = angle1 - angle2;
                adjustLength = (float)(oppositeSideLength * Math.Cos(targetAngle)) - shape.Width;
            }
            shape.Left += adjustLength;
        }

        private float SetupRotationValueForAdjustment(ref PowerPoint.Shape shape)
        {
            float rotation = shape.Rotation;
            if (shape.Rotation > 180 && shape.Rotation <= 360)
            {
                rotation = 360 - shape.Rotation;
            }
            return ConvertDegToRad(rotation);
        }

        private float GetRotationFactorForFitToWidth(ref PowerPoint.Shape shape)
        {
            float rotation = SetupRotationValueForRotateFactor(ref shape);
            //calculate factor for Fit to Height
            float shapeSizeRatio = GetSizeRatio(shape.Height, shape.Width);
            float factor = (float)(Math.Sin(rotation) + Math.Cos(rotation) / shapeSizeRatio);
            return factor;
        }

        private float SetupRotationValueForRotateFactor(ref PowerPoint.Shape shape)
        {
            float rotation;
            if (shape.Rotation == 90.0)
            {
                rotation = shape.Rotation;
            }
            else if (shape.Rotation == 270.0)
            {
                rotation = 360 - shape.Rotation;
            }
            else if ((shape.Rotation > 90 && shape.Rotation <= 180)
                     || (shape.Rotation > 270 && shape.Rotation <= 360))
            {
                rotation = (360 - shape.Rotation) % 90;
            }
            else
            {
                rotation = shape.Rotation % 90;
            }
            return ConvertDegToRad(rotation);
        }

        private float ConvertDegToRad(float rotation)
        {
            rotation = (float)((rotation) * Math.PI / 180); return rotation;
        }

        private float GetSizeRatio(float height, float width)
        {
            return height / width;
        }

        private PowerPoint.PageSetup GetPageSetup()
        {
            return Globals.ThisAddIn.Application.ActivePresentation.PageSetup;
        }

        private PowerPoint.Shape GetSelectedShape()
        {
            return Globals.ThisAddIn.Application.
                   ActiveWindow.Selection.ShapeRange[SelectedShapeIndex];
        }

        public System.Drawing.Bitmap GetFitToWidthImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.FitToWidth);
            }
            catch (Exception e)
            {
                LogException(e, "GetFitToWidthImage");
                throw;
            }
        }

        public System.Drawing.Bitmap GetFitToHeightImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.FitToHeight);
            }
            catch (Exception e)
            {
                LogException(e, "GetFitToHeightImage");
                throw;
            }
        }

        #endregion

        #region feature: Crop to Shape

        private void CropShape(ref PowerPoint.Selection selection)
        {
            try
            {
                IsValidSelectionForCropToShape(ref selection);
                var shape = FormShapeForCropToShape(ref selection);
                TakeScreenshotWithoutShape(ref shape);
                FillInShapeWithScreenshot(shape);
            }
            catch (Exception)
            {
                MessageBox.Show(GetCropToShapeErrorMessage(), "Unable to crop");
                CropToShapeErrorCode = -1;
            }
        }

        private int CropToShapeErrorCode = -1;
        private const int ERROR_SELECTION_COUNT_ZERO = 0;
        private const int ERROR_SELECTION_NON_SHAPE = 1;
        private const int ERROR_EXCEED_SLIDE_BOUND = 2;
        private const int ERROR_ROTATION_NON_ZERO = 3;
        const string OfficeVersion2013 = "15.0";
        const string OfficeVersion2010 = "14.0";

        private string GetCropToShapeErrorMessage()
        {
            switch (CropToShapeErrorCode)
            {
                case ERROR_SELECTION_COUNT_ZERO:
                    return "To start 'Auto Crop', please select at least one shape.";
                case ERROR_SELECTION_NON_SHAPE:
                    return "'Auto Crop' only supports shape objects.";
                case ERROR_EXCEED_SLIDE_BOUND:
                    return "Please ensure your shape is within the slide.";
                case ERROR_ROTATION_NON_ZERO:
                    return "Please ensure your shape is unrotated.";
                default:
                    return "Undefined error.";                    
            }
        }

        private void FillInShapeWithScreenshot(PowerPoint.Shape shape)
        {
            var fillEffect = shape.Fill;
            if (Globals.ThisAddIn.Application.Version == OfficeVersion2013
                || (Globals.ThisAddIn.Application.Version == OfficeVersion2010
                && shape.Type != Office.MsoShapeType.msoGroup))
            {
                ProduceFillInBackground(ref shape);
                fillEffect.UserPicture(GetPathForFillInBackground());                
                shape.Select();
            }
            else if(Globals.ThisAddIn.Application.Version == OfficeVersion2010)
            {
                fillEffect.UserTextured(GetPathToStore());
                fillEffect.TextureOffsetX = -shape.Left;
                fillEffect.TextureOffsetY = -shape.Top;
                AdjustFillEffect(ref shape);
                shape.Line.Visible = Office.MsoTriState.msoFalse;
                var pic = ConvertToPicture(ref shape);
                shape = pic;
            }
            shape.Line.Weight = 3;//arbitrary slightly large weight
            shape.Line.ForeColor.RGB = 0xffffff;
            shape.Line.Visible = Office.MsoTriState.msoTrue;
        }

        private void AdjustFillEffect(ref PowerPoint.Shape shape)
        {
            if (shape.Type == Office.MsoShapeType.msoGroup)
            {
                var range = shape.Ungroup();
                foreach (var o in range)
                {
                    var sh = o as PowerPoint.Shape;
                    var tmpFillEffect = sh.Fill;
                    tmpFillEffect.TextureOffsetX = -sh.Left;
                    tmpFillEffect.TextureOffsetY = -sh.Top;
                }
                shape = range.Group();
            }
        }

        private void ProduceFillInBackground(ref PowerPoint.Shape shape)
        {
            using (Bitmap slideImage = (Bitmap)Bitmap.FromFile(GetPathToStore()))
            {
                float horizontalRatio =
                    (float)(GetDesiredExportWidth() / Globals.ThisAddIn.Application.ActivePresentation.PageSetup.SlideWidth);
                float verticalRatio =
                    (float)(GetDesiredExportHeight() / Globals.ThisAddIn.Application.ActivePresentation.PageSetup.SlideHeight);
                Bitmap croppedImage = KiCut(slideImage,
                    shape.Left * horizontalRatio,
                    shape.Top * verticalRatio,
                    shape.Width * horizontalRatio,
                    shape.Height * verticalRatio);
                croppedImage.Save(GetPathForFillInBackground(), ImageFormat.Png);
            }
        }

        public static Bitmap KiCut(Bitmap original, float startX, float startY, float width, float height)
        {
            if (original == null)
            {
                return null;
            }
            if (startX >= original.Width || startY >= original.Height)
            {
                return null;
            }
            try
            {
                Bitmap outputImage = new Bitmap((int)width, (int)height, PixelFormat.Format24bppRgb);

                Graphics inputGraphics = Graphics.FromImage(outputImage);
                inputGraphics.DrawImage(original,
                    new Rectangle(0, 0, (int)width, (int)height),
                    new Rectangle((int)startX, (int)startY, (int)width, (int)height),
                    GraphicsUnit.Pixel);
                inputGraphics.Dispose();

                return outputImage;
            }
            catch
            {
                return null;
            }
        }

        private string GetPathForFillInBackground()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
            return path + "\\currentFillInBg.png";
        }

        private string GetPathToStore()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
            return path + "\\currentSlide.png";
        }

        private PowerPoint.Shape ConvertToPicture(ref PowerPoint.Shape shape)
        {
            shape.Copy();
            float x = shape.Left;
            float y = shape.Top;
            shape.Delete();
            var pic = GetCurrentSlide().Shapes.PasteSpecial(PowerPoint.PpPasteDataType.ppPastePNG)[1];
            pic.Left = x;
            pic.Top = y;
            pic.Select();
            return pic;
        }

        private bool IsFirstOneOverlapWithSecond(PowerPoint.Shape first, PowerPoint.Shape second)
        {
            if (first == second)
                return false;
            if (!(first.Left > second.Left + second.Width ||
                  first.Left + first.Width < second.Left ||
                  first.Top > second.Top + second.Height ||
                  first.Top + first.Height < second.Top))
            {
                return true;
            }
            return false;
        }

        private void RemoveOverlapItems(PowerPoint.Shape shape)
        {
            var shapeRange = GetCurrentSlide().Shapes;
            for (int i = 1; i < shapeRange.Count; )
            {
                if (IsFirstOneOverlapWithSecond(shapeRange[i], shape))
                    shapeRange[i].Delete();
                else
                    i++;
            }
        }

        private void TakeScreenshotWithoutShape(ref PowerPoint.Shape shape)
        {
            shape.Visible = Office.MsoTriState.msoFalse;
            TakeScreenshot();
            shape.Visible = Office.MsoTriState.msoTrue;
        }

        private void TakeScreenshot()
        {
            GetCurrentSlide().Export(GetPathToStore(), "PNG", 
                (int)GetDesiredExportWidth(), (int)GetDesiredExportHeight());
        }

        private double GetDesiredExportWidth()
        {
            return Globals.ThisAddIn.Application.ActivePresentation.PageSetup.SlideWidth / 72.0 * 96.0;
        }

        private double GetDesiredExportHeight()
        {
            return Globals.ThisAddIn.Application.ActivePresentation.PageSetup.SlideHeight / 72.0 * 96.0;
        }

        private PowerPoint.Shape FormShapeForCropToShape(ref PowerPoint.Selection selection)
        {
            var oldRange = selection.ShapeRange;
            //some shapes in the nameList cannot be used due to 
            //Powerpoint's 'Delete-Undo' bug: when a shape got deleted or cut, then undo,
            //then we can only read its name/width/height/left/top.. for others, it aborts
            var nameList = ProduceNameList(GetCurrentSlide().Shapes.Range());
            //'Cut-Paste' is a normal workaround method for the bug mentioned above
            oldRange.Cut();
            oldRange = GetCurrentSlide().Shapes.Paste();
            var copyRange = MakeCopyWithSamePosition(oldRange);
            var ungroupedCopyRange = UngroupAllShapes(copyRange);
            PowerPoint.Shape mergedShape = ungroupedCopyRange[1];
            if (Globals.ThisAddIn.Application.Version == OfficeVersion2013)
            {
                mergedShape = MergeAllShapes(ref ungroupedCopyRange, nameList);
                RemoveRotation(ref mergedShape, nameList);
            }
            else if (Globals.ThisAddIn.Application.Version == OfficeVersion2010)
            {
                if (ungroupedCopyRange.Count > 1)
                {
                    mergedShape = ungroupedCopyRange.Group();
                }
            }
            if (IsWithinSlide(mergedShape))
            {
                oldRange.Delete();
            }
            else
            {
                mergedShape.Delete();
                ThrowExceptionFromCropToShape(ERROR_EXCEED_SLIDE_BOUND);
            }
            return mergedShape;
        }

        private PowerPoint.ShapeRange MakeCopyWithSamePosition(PowerPoint.ShapeRange oldRange)
        {
            //setup shapes' names in oldRange, so that shapes' names in copyRange is the same
            ModifyNames(oldRange);
            oldRange.Copy();
            var copyRange = GetCurrentSlide().Shapes.Paste();
            //adjust position
            Dictionary<string, Tuple<float,float>> dict = new Dictionary<string, Tuple<float,float>>();
            foreach (var sh in oldRange)
            {
                var shape = sh as PowerPoint.Shape;
                dict.Add(shape.Name, new Tuple<float, float>(shape.Left, shape.Top));
            }
            foreach (var sh in copyRange)
            {
                var shape = sh as PowerPoint.Shape;
                shape.Left = dict[shape.Name].Item1;
                shape.Top = dict[shape.Name].Item2;
            }
            //append something in case we want to differentiate them
            ModifyNames(copyRange, "_Copy");
            return copyRange;
        }

        private List<string> ProduceNameList(PowerPoint.ShapeRange range)
        {
            List<string> output = new List<string>();
            foreach (var sh in range)
            {
                output.Add((sh as PowerPoint.Shape).Name);
            }
            return output;
        }

        private void ModifyNames(PowerPoint.ShapeRange range, string appendString = "")
        {
            if (appendString != "")
            {
                foreach (var sh in range)
                {
                    (sh as PowerPoint.Shape).Name += appendString;
                }
            }
            else
            {
                foreach (var sh in range)
                {
                    (sh as PowerPoint.Shape).Name = Guid.NewGuid().ToString();
                }
            }
        } 

        private bool IsWithinSlide(PowerPoint.Shape shape)
        {
            //-1 and +1 for better user experience
            bool cond1 = shape.Left >= -1;
            bool cond2 = shape.Top >= -1;
            bool cond3 = shape.Left + shape.Width <= Globals.ThisAddIn.Application.ActivePresentation.PageSetup.SlideWidth + 1;
            bool cond4 = shape.Top + shape.Height <= Globals.ThisAddIn.Application.ActivePresentation.PageSetup.SlideHeight + 1;
            return cond1 && cond2 && cond3 && cond4;
        }

        private PowerPoint.ShapeRange UngroupAllShapes(PowerPoint.ShapeRange range)
        {
            List<string> ungroupedShapes = new List<string>();
            Queue<PowerPoint.Shape> selectedShapes = new Queue<PowerPoint.Shape>();

            foreach (var item in range)
            {
                selectedShapes.Enqueue(item as PowerPoint.Shape);
            }
            while (selectedShapes.Count != 0)
            {
                var shape = selectedShapes.Dequeue();
                if (shape.Type == Office.MsoShapeType.msoGroup)
                {
                    var subRange = shape.Ungroup();
                    foreach (var item in subRange)
                    {
                        selectedShapes.Enqueue(item as PowerPoint.Shape);
                    }
                }
                else if (Globals.ThisAddIn.Application.Version == OfficeVersion2010
                    && shape.Rotation != 0.0)
                {
                    //remove copies before throwing exception
                    shape.Delete();
                    if (ungroupedShapes.Count > 0)
                    {
                        GetCurrentSlide().Shapes.Range(ungroupedShapes.ToArray()).Delete();
                    }
                    while (selectedShapes.Count != 0)
                    {
                        selectedShapes.Dequeue().Delete();
                    }
                    ThrowExceptionFromCropToShape(ERROR_ROTATION_NON_ZERO);
                }
                else if (!IsShape(shape))
                {
                    //remove copies before throwing exception
                    shape.Delete();
                    if (ungroupedShapes.Count > 0)
                    {
                        GetCurrentSlide().Shapes.Range(ungroupedShapes.ToArray()).Delete();
                    }
                    while (selectedShapes.Count != 0)
                    {
                        selectedShapes.Dequeue().Delete();
                    }
                    ThrowExceptionFromCropToShape(ERROR_SELECTION_NON_SHAPE);
                }
                else
                {
                    shape.Name = Guid.NewGuid().ToString();
                    ungroupedShapes.Add(shape.Name);
                }
            }
            return GetCurrentSlide().Shapes.Range(ungroupedShapes.ToArray());
        }

        private PowerPoint.Shape MergeAllShapes(ref PowerPoint.ShapeRange range, List<string> nameList)
        {
            PowerPoint.Shape shape = range[1];
            if (range.Count > 1)
            {
                int KaisBirthday = 0x900209;
                range[1].Line.ForeColor.RGB = KaisBirthday;
                //merged shape will inherit the foreColor of range[1]'s line,
                //which is Xie Kai's birthday :p
                //a better choice can be: random number or timestamp
                //range.MergeShapes(Office.MsoMergeCmd.msoMergeUnion, range[1]);
                //find the merged shape
                var newRange = GetCurrentSlide().Shapes.Range();
                foreach (var sh in newRange)
                {
                    var mergedShape = sh as PowerPoint.Shape;
                    if (!nameList.Contains(mergedShape.Name))
                    {
                        if(KaisBirthday == mergedShape.Line.ForeColor.RGB)
                        {
                            shape = mergedShape;
                        }
                    }
                }
            }
            return shape;
        }

        private void RemoveRotation(ref PowerPoint.Shape shape, List<string> nameList)
        {
            var helperShape = GetCurrentSlide().Shapes.AddShape(Office.MsoAutoShapeType.msoShapeRectangle, -100, -100, 1, 1);
            int KaisBirthday = 0x900209;
            helperShape.Line.ForeColor.RGB = KaisBirthday;
            //merged shape will inherit the foreColor of range[1]'s line,
            //which is Xie Kai's birthday :p
            //a better choice can be: random number or timestamp
            var range = GetCurrentSlide().
                Shapes.Range(new List<string> { helperShape.Name, shape.Name }.ToArray());
            //Separate the shapes and make rotation back to zero
            //range.MergeShapes(Office.MsoMergeCmd.msoMergeFragment, helperShape);
            //find those resulted shapes
            var newRange = GetCurrentSlide().Shapes.Range();
            List<string> list = new List<string>();
            foreach (var sh in newRange)
            {
                var mergedShape = sh as PowerPoint.Shape;
                if (mergedShape.Left == -100
                    && mergedShape.Top == -100
                    && mergedShape.Width == 1
                    && mergedShape.Height == 1)
                {
                    helperShape = mergedShape;
                }
                else if (!nameList.Contains(mergedShape.Name))
                {
                    if(KaisBirthday == mergedShape.Line.ForeColor.RGB)
                    {
                        list.Add(mergedShape.Name);
                    }
                }
            }
            helperShape.Delete();
            range = GetCurrentSlide().Shapes.Range(list.ToArray());
            if (list.Count > 1)
            {
                //range.MergeShapes(Office.MsoMergeCmd.msoMergeUnion);
                //find out the merged shape
                newRange = GetCurrentSlide().Shapes.Range();
                foreach (var sh in newRange)
                {
                    var mergedShape = sh as PowerPoint.Shape;
                    if (!nameList.Contains(mergedShape.Name))
                    {
                        if (KaisBirthday == mergedShape.Line.ForeColor.RGB)
                        {
                            shape = mergedShape;
                        }
                    }
                }
            }
            else
            {
                shape = range[1];
            }
        }

        private void ThrowExceptionFromCropToShape(int typeOfError)
        {
            CropToShapeErrorCode = typeOfError;
            throw new Exception("Error: " + typeOfError.ToString());
        }

        private void IsValidSelectionForCropToShape(ref PowerPoint.Selection selection)
        {
            if (selection.Type == PowerPoint.PpSelectionType.ppSelectionShapes)
            {
                if (selection.ShapeRange.Count < 1)
                {
                    ThrowExceptionFromCropToShape(ERROR_SELECTION_COUNT_ZERO);
                }
                if (!IsShape(selection))
                {
                    ThrowExceptionFromCropToShape(ERROR_SELECTION_NON_SHAPE);
                }
            }
            else
            {
                ThrowExceptionFromCropToShape(ERROR_SELECTION_COUNT_ZERO);
            }
        }

        private bool IsShape(PowerPoint.Selection sel)
        {
            var shapeRange = sel.ShapeRange;
            foreach (var o in shapeRange)
            {
                var shape = o as PowerPoint.Shape;
                if (!IsShape(shape))
                    return false;
            }
            return true;
        }

        private bool IsShape(PowerPoint.Shape shape)
        {
            if (shape.Type != Office.MsoShapeType.msoAutoShape
                    && shape.Type != Office.MsoShapeType.msoFreeform
                    && shape.Type != Office.MsoShapeType.msoGroup)
            {
                return false;
            }
            return true;
        }

        public System.Drawing.Bitmap GetCutOutShapeMenuImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.CutOutShapeMenu);
            }
            catch (Exception e)
            {
                LogException(e, "GetCutOutShapeMenuImage");
                throw;
            }
        }

        #endregion

        #region feature: Conver to Picture

        public void ConvertToPictureButtonClick(Office.IRibbonControl control)
        {
            var selection = Globals.ThisAddIn.Application.
                ActiveWindow.Selection;
            if (selection.Type == PowerPoint.PpSelectionType.ppSelectionShapes)
            {
                var shape = selection.ShapeRange[1];
                ConvertToPicture(ref shape);
            }
            else
            {
                MessageBox.Show("Convert to Picture only supports Shapes and Charts.", "Unable to Convert to Picture");
            }
        }

        public System.Drawing.Bitmap GetConvertToPicMenuImage(Office.IRibbonControl control)
        {
            try
            {
                return new System.Drawing.Bitmap(Properties.Resources.ConvertToPicture);
            }
            catch (Exception e)
            {
                LogException(e, "GetConvertToPicMenuImage");
                throw;
            }
        }

        #endregion

        #region Helpers

        //Spotlight Helpers
        

        //AutoAnimate Helpers
        private void AddCompleteAutoMotion(PowerPoint.Slide currentSlide, PowerPoint.Slide nextSlide)
        {
            try
            {
                //Create containers to store information on matching shapes
                PowerPoint.Shape[] shapes1;
                PowerPoint.Shape[] shapes2;
                int[] shapeIDs;

                if (GetMatchingShapeDetails(currentSlide, nextSlide, out shapes1, out shapes2, out shapeIDs))
                {
                    //If an identical object exists
                    AboutForm progressForm = new AboutForm();
                    progressForm.Visible = true;
                    PowerPoint.Slide newSlide = PrepareAnimatedSlide(currentSlide, shapeIDs);
                    AddAnimationsToShapes(newSlide, shapes1, shapes2, shapeIDs);
                    //this.ribbon.ActivateTabMso("TabAnimations");
                    Globals.ThisAddIn.Application.CommandBars.ExecuteMso("AnimationPreview");
                    progressForm.Visible = false;
                }
                else
                {
                    //Display error message
                    System.Windows.Forms.MessageBox.Show("No matching Shapes were found on the next slide", "Animation Not Added");
                }
            }
            catch (Exception e)
            {
                LogException(e, "AddCompleteAutoMotion");
                throw;
            }
        }
        private void AddAnimationsToShapes(PowerPoint.Slide newSlide, PowerPoint.Shape[] shapes1, PowerPoint.Shape[] shapes2, int[] shapeIDs)
        {
            try
            {
                int count = 0;
                bool fadeFlag = false;
                PowerPoint.Sequence sequence = newSlide.TimeLine.MainSequence;
                PowerPoint.Effect effectMotion = null;
                PowerPoint.Effect effectResize = null;
                PowerPoint.Effect effectRotate = null;
                //PowerPoint.Effect effectFontResize = null;
                PowerPoint.Effect effectFade = null;
                PowerPoint.Effect effectDisappear = null;
                PowerPoint.MsoAnimTriggerType trigger = PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious;
                PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;

                String tempFileName = Path.GetTempFileName();
                Properties.Resources.Indicator.Save(tempFileName);
                PowerPoint.Shape indicatorShape = newSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, presentation.PageSetup.SlideWidth - 120, 0, 120, 84);
                indicatorShape.Left = presentation.PageSetup.SlideWidth - 120;
                indicatorShape.Top = 0;
                indicatorShape.Width = 120;
                indicatorShape.Height = 84;
                indicatorShape.Name = "PPIndicator" + GetTimestamp(DateTime.Now);
                effectDisappear = sequence.AddEffect(indicatorShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                effectDisappear.Exit = Office.MsoTriState.msoTrue;
                effectDisappear.Timing.Duration = 0;
                int indicatorID = indicatorShape.Id;

                foreach (PowerPoint.Shape sh in newSlide.Shapes)
                {
                    if (!shapeIDs.Contains(sh.Id) && sh.Id != indicatorID)
                    {
                        //sh.Delete();
                        if (DeleteShapeAnnimations(newSlide, sh))
                        {
                            effectFade = sequence.AddEffect(sh, PowerPoint.MsoAnimEffect.msoAnimEffectFade, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                            effectFade.Exit = Office.MsoTriState.msoTrue;
                            effectFade.Timing.Duration = defaultDuration;
                            fadeFlag = true;
                        }
                        else
                        {
                            PowerPoint.Effect effectDisappear2 = null;
                            effectDisappear2 = sequence.AddEffect(sh, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                            effectDisappear2.Exit = Office.MsoTriState.msoTrue;
                            effectDisappear2.Timing.Duration = 0;
                        }
                    }
                }

                //Add animation effects to the duplicated objects
                foreach (PowerPoint.Shape sh in newSlide.Shapes)
                {
                    if (shapeIDs.Contains(sh.Id))
                    {
                        count = Array.IndexOf(shapeIDs, sh.Id);
                        if (count < shapeIDs.Count() && sh.Id == shapeIDs[count])
                        {
                            DeleteShapeAnnimations(newSlide, sh);
                            trigger = (count == 0 && fadeFlag) ? PowerPoint.MsoAnimTriggerType.msoAnimTriggerAfterPrevious : PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious;
                            float finalX = (shapes2[count].Left + (shapes2[count].Width) / 2);
                            float initialX = (sh.Left + (sh.Width) / 2);
                            float finalY = (shapes2[count].Top + (shapes2[count].Height) / 2);
                            float initialY = (sh.Top + (sh.Height) / 2);

                            float finalRotation = shapes2[count].Rotation;
                            float initialRotation = sh.Rotation;

                            float finalWidth = shapes2[count].Width;
                            float initialWidth = sh.Width;
                            float finalHeight = shapes2[count].Height;
                            float initialHeight = sh.Height;
                            float finalFont = 0.0f;
                            float initialFont = 0.0f;
                            int numFrames = (int)(defaultDuration / 0.04f);
                            numFrames = (numFrames > 30) ? 30 : numFrames;

                            if (sh.HasTextFrame == Office.MsoTriState.msoTrue && (sh.TextFrame.HasText == Office.MsoTriState.msoTriStateMixed || sh.TextFrame.HasText == Office.MsoTriState.msoTrue) && sh.TextFrame.TextRange.Font.Size != shapes2[count].TextFrame.TextRange.Font.Size)
                            {
                                finalFont = shapes2[count].TextFrame.TextRange.Font.Size;
                                initialFont = sh.TextFrame.TextRange.Font.Size;
                            }

                            if ((frameAnimationChecked && (finalHeight != initialHeight || finalWidth != initialWidth))
                                || ((initialRotation != finalRotation || initialRotation % 90 != 0) && (finalHeight != initialHeight || finalWidth != initialWidth))
                                || finalFont != initialFont)
                            {
                                float incrementWidth = ((finalWidth / initialWidth) - 1.0f) / numFrames;
                                float incrementHeight = ((finalHeight / initialHeight) - 1.0f) / numFrames;
                                float incrementRotation = GetMinimumRotation(initialRotation, finalRotation) / numFrames;
                                float incrementLeft = (finalX - initialX) / numFrames;
                                float incrementTop = (finalY - initialY) / numFrames;
                                float incrementFont = (finalFont - initialFont) / numFrames;

                                PowerPoint.Shape lastShape = sh;
                                for (int i = 1; i <= numFrames; i++)
                                {
                                    PowerPoint.Shape dupShape = sh.Duplicate()[1];
                                    if (i != 1)
                                        sequence[sequence.Count].Delete();

                                    dupShape.LockAspectRatio = Office.MsoTriState.msoFalse;
                                    dupShape.Left = sh.Left;
                                    dupShape.Top = sh.Top;

                                    if (incrementWidth != 0.0f)
                                    {
                                        dupShape.ScaleWidth((1.0f + (incrementWidth * i)), Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromMiddle);
                                    }

                                    if (incrementHeight != 0.0f)
                                    {
                                        dupShape.ScaleHeight((1.0f + (incrementHeight * i)), Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromMiddle);
                                    }

                                    if (incrementRotation != 0.0f)
                                    {
                                        dupShape.Rotation += (incrementRotation * i);
                                    }

                                    if (incrementLeft != 0.0f)
                                    {
                                        dupShape.Left += (incrementLeft * i);
                                    }

                                    if (incrementTop != 0.0f)
                                    {
                                        dupShape.Top += (incrementTop * i);
                                    }

                                    if (incrementFont != 0.0f)
                                    {
                                        dupShape.TextFrame.TextRange.Font.Size += (incrementFont * i);
                                    }

                                    PowerPoint.Effect appear = sequence.AddEffect(dupShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                                    //appear.Timing.Duration = 0.005f;
                                    appear.Timing.TriggerDelayTime = ((defaultDuration / numFrames) * i);

                                    PowerPoint.Effect disappear = sequence.AddEffect(lastShape, PowerPoint.MsoAnimEffect.msoAnimEffectAppear, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious);
                                    disappear.Exit = Office.MsoTriState.msoTrue;
                                    //disappear.Timing.Duration = 0.005f;
                                    disappear.Timing.TriggerDelayTime = ((defaultDuration / numFrames) * i);

                                    lastShape = dupShape;
                                }
                            }
                            else
                            {
                                //Motion Effect
                                if ((finalX != initialX) || (finalY != initialY))
                                {
                                    effectMotion = sequence.AddEffect(sh, PowerPoint.MsoAnimEffect.msoAnimEffectPathDown, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, trigger);
                                    //PowerPoint.AnimationBehavior motion = effectMotion.Behaviors.Add(PowerPoint.MsoAnimType.msoAnimTypeMotion);
                                    PowerPoint.AnimationBehavior motion = effectMotion.Behaviors[1];
                                    effectMotion.Timing.Duration = defaultDuration;
                                    trigger = PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious;
                                    //effectMotion.EffectParameters.Relative = Office.MsoTriState.msoTrue;
                                    //motion.MotionEffect.FromX = initialX / presentation.PageSetup.SlideWidth * 100;
                                    //motion.MotionEffect.FromY = initialY / presentation.PageSetup.SlideHeight * 100;
                                    //motion.MotionEffect.ToX = (finalX - initialX) / presentation.PageSetup.SlideWidth * 100;
                                    //motion.MotionEffect.ToY = (finalY - initialY) / presentation.PageSetup.SlideHeight * 100;

                                    //Create VML path for the motion path
                                    //This path needs to be a curved path to allow the user to edit points
                                    float point1X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
                                    float point1Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
                                    float point2X = ((finalX - initialX) / 2f) / presentation.PageSetup.SlideWidth;
                                    float point2Y = ((finalY - initialY) / 2f) / presentation.PageSetup.SlideHeight;
                                    float point3X = (finalX - initialX) / presentation.PageSetup.SlideWidth;
                                    float point3Y = (finalY - initialY) / presentation.PageSetup.SlideHeight;
                                    motion.MotionEffect.Path = "M 0 0 C " + point1X + " " + point1Y + " " + point2X + " " + point2Y + " " + point3X + " " + point3Y + " E";
                                    effectMotion.Timing.SmoothStart = Office.MsoTriState.msoFalse;
                                    effectMotion.Timing.SmoothEnd = Office.MsoTriState.msoFalse;
                                }

                                //Resize Effect
                                //if (sh.Type != Office.MsoShapeType.msoPlaceholder && sh.Type != Office.MsoShapeType.msoTextBox)
                                //{
                                if ((finalWidth != initialWidth) || (finalHeight != initialHeight))
                                {
                                    sh.LockAspectRatio = Office.MsoTriState.msoFalse;
                                    effectResize = sequence.AddEffect(sh, PowerPoint.MsoAnimEffect.msoAnimEffectGrowShrink, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, trigger);
                                    //PowerPoint.AnimationBehavior resize = effectResize.Behaviors.Add(PowerPoint.MsoAnimType.msoAnimTypeScale);
                                    PowerPoint.AnimationBehavior resize = effectResize.Behaviors[1];

                                    //float rotCos = (float)Math.Cos(degToRad(sh.Rotation));
                                    //float rotSin = (float)Math.Sin(degToRad(sh.Rotation));

                                    effectResize.Timing.Duration = defaultDuration;
                                    //sh.ScaleWidth((finalWidth / initialWidth), Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromMiddle);
                                    //sh.ScaleHeight((finalHeight / initialHeight), Office.MsoTriState.msoFalse, Office.MsoScaleFrom.msoScaleFromMiddle);

                                    resize.ScaleEffect.ByX = (finalWidth / initialWidth) * 100;
                                    resize.ScaleEffect.ByY = (finalHeight / initialHeight) * 100;

                                    //resize.ScaleEffect.ByX = (((finalWidth / initialWidth) * Math.Abs(rotCos)) + ((finalHeight / initialHeight) * Math.Abs(rotSin))) * 100;
                                    //resize.ScaleEffect.ByY = (((finalWidth / initialWidth) * Math.Abs(rotSin)) + ((finalHeight / initialHeight) * Math.Abs(rotCos))) * 100;
                                    trigger = PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious;
                                }
                                //}
                                //if (sh.HasTextFrame == Office.MsoTriState.msoTrue && sh.TextFrame.HasText == Office.MsoTriState.msoTrue && sh.TextFrame.TextRange.Font.Size != shapes2[count].TextFrame.TextRange.Font.Size)
                                //{
                                //    sh.TextFrame.WordWrap = Office.MsoTriState.msoTrue;
                                //    effectFontResize = sequence.AddEffect(sh, PowerPoint.MsoAnimEffect.msoAnimEffectChangeFontSize, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, trigger);
                                //    effectFontResize.Timing.Duration = defaultDuration;
                                //    PowerPoint.AnimationBehavior resizeFont = effectFontResize.Behaviors[1];
                                //    resizeFont.PropertyEffect.To = shapes2[count].TextFrame.TextRange.Font.Size / shapes1[count].TextFrame.TextRange.Font.Size;
                                //    trigger = PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious;
                                //}

                                //Rotation Effect
                                if (finalRotation != initialRotation)
                                {
                                    effectRotate = sequence.AddEffect(sh, PowerPoint.MsoAnimEffect.msoAnimEffectSpin, PowerPoint.MsoAnimateByLevel.msoAnimateLevelNone, trigger);
                                    PowerPoint.AnimationBehavior rotate = effectRotate.Behaviors[1];
                                    effectRotate.Timing.Duration = defaultDuration;
                                    effectRotate.EffectParameters.Amount = GetMinimumRotation(initialRotation, finalRotation);
                                    trigger = PowerPoint.MsoAnimTriggerType.msoAnimTriggerWithPrevious;
                                }
                            }
                            count++;
                        }
                    }
                }
                indicatorShape.ZOrder(Office.MsoZOrderCmd.msoBringToFront);
            }
            catch (Exception e)
            {
                LogException(e, "AddAnimationsToShapes");
                throw;
            }
        }
        private PowerPoint.Slide PrepareAnimatedSlide(PowerPoint.Slide currentSlide, int[] shapeIDs)
        {
            try
            {
                //Duplicate current slide
                currentSlide.Duplicate();
                if (currentSlide.Name.Contains("PPSlideEnd") || currentSlide.Name.Contains("PPSlideMulti"))
                    currentSlide.Name = "PPSlideMulti" + GetTimestamp(DateTime.Now);
                else
                    currentSlide.Name = "PPSlideStart" + GetTimestamp(DateTime.Now);
                //Globals.ThisAddIn.Application.ActivePresentation.Slides.AddSlide(currentSlide.SlideIndex + 1, currentSlide.CustomLayout);

                //Store reference of new slide
                PowerPoint.Slide newSlide = GetNextSlide(currentSlide);
                newSlide.Name = "PPSlideAnimated" + GetTimestamp(DateTime.Now);

                //Go to new slide
                Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(newSlide.SlideIndex);
                PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;

                //Delete non-identical shapes
                foreach (PowerPoint.Effect eff in newSlide.TimeLine.MainSequence)
                {
                    if ((eff.EffectType >= PowerPoint.MsoAnimEffect.msoAnimEffectPathCircle && eff.EffectType <= PowerPoint.MsoAnimEffect.msoAnimEffectPathRight) || eff.EffectType == PowerPoint.MsoAnimEffect.msoAnimEffectCustom)
                    {
                        //sh.Delete();
                        PowerPoint.AnimationBehavior motion = eff.Behaviors[1];
                        if (motion.Type == PowerPoint.MsoAnimType.msoAnimTypeMotion)
                        {
                            PowerPoint.Shape sh = eff.Shape;
                            string motionPath = motion.MotionEffect.Path.Trim();
                            if (motionPath.Last() < 'A' || motionPath.Last() > 'Z')
                                motionPath += " X";
                            string[] path = motionPath.Split(' ');
                            int count = path.Length;
                            float xVal = Convert.ToSingle(path[count - 3]);
                            float yVal = Convert.ToSingle(path[count - 2]);
                            sh.Left += (xVal * presentation.PageSetup.SlideWidth);
                            sh.Top += (yVal * presentation.PageSetup.SlideHeight);
                        }
                    }
                }

                //Manage Slide Transitions
                newSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
                //newSlide.SlideShowTransition.Duration = defaultDuration;
                newSlide.SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoTrue;
                newSlide.SlideShowTransition.AdvanceOnClick = Office.MsoTriState.msoFalse;
                newSlide.SlideShowTransition.AdvanceTime = 0;
                if (newSlide.HasNotesPage == Office.MsoTriState.msoTrue)
                {
                    foreach (PowerPoint.Shape sh in newSlide.NotesPage.Shapes)
                    {
                        if (sh.TextFrame.HasText == Office.MsoTriState.msoTrue)
                            sh.TextEffect.Text = "";
                    }
                }

                foreach (PowerPoint.Shape sh in newSlide.Shapes)
                {
                    if (sh.Type == Office.MsoShapeType.msoMedia)
                        sh.Delete();
                }

                PowerPoint.Slide nextSlide = GetNextSlide(newSlide);
                if (nextSlide.SlideShowTransition.EntryEffect != PowerPoint.PpEntryEffect.ppEffectFade && nextSlide.SlideShowTransition.EntryEffect != PowerPoint.PpEntryEffect.ppEffectFadeSmoothly)
                    nextSlide.SlideShowTransition.EntryEffect = PowerPoint.PpEntryEffect.ppEffectNone;
                if (nextSlide.Name.Contains("PPSlideStart") || nextSlide.Name.Contains("PPSlideMulti"))
                    nextSlide.Name = "PPSlideMulti" + GetTimestamp(DateTime.Now);
                else
                    nextSlide.Name = "PPSlideEnd" + GetTimestamp(DateTime.Now);

                AddAckSlide();

                return newSlide;
            }
            catch (Exception e)
            {
                LogException(e, "PrepareAnimatedSlide");
                throw;
            }
        }
        private bool GetMatchingShapeDetails(PowerPoint.Slide currentSlide, PowerPoint.Slide nextSlide, out PowerPoint.Shape[] shapes1, out PowerPoint.Shape[] shapes2, out int[] shapeIDs)
        {
            try
            {
                bool flag = false;
                int counter = 0;

                shapes1 = new PowerPoint.Shape[currentSlide.Shapes.Count];
                shapes2 = new PowerPoint.Shape[currentSlide.Shapes.Count];
                shapeIDs = new int[currentSlide.Shapes.Count];
                PowerPoint.Shape matchingShape;

                foreach (PowerPoint.Shape sh1 in currentSlide.Shapes)
                {
                    matchingShape = null;
                    foreach (PowerPoint.Shape sh2 in nextSlide.Shapes)
                    {
                        if (sh1.Id == sh2.Id && haveSameNames(sh1, sh2))
                        {
                            if (matchingShape == null)
                                matchingShape = sh2;
                            else
                            {
                                if (GetDistanceBetweenShapes(sh1, sh2) < GetDistanceBetweenShapes(sh1, matchingShape))
                                    matchingShape = sh2;
                            }
                            flag = true;
                        }
                    }
                    if (matchingShape == null)
                    {
                        foreach (PowerPoint.Shape sh2 in nextSlide.Shapes)
                        {
                            if (haveSameNames(sh1, sh2))
                            {
                                if (matchingShape == null)
                                    matchingShape = sh2;
                                else
                                {
                                    if (GetDistanceBetweenShapes(sh1, sh2) < GetDistanceBetweenShapes(sh1, matchingShape))
                                        matchingShape = sh2;
                                }
                                flag = true;
                            }
                        }
                    }
                    if (matchingShape != null)
                    {
                        shapes1[counter] = sh1;
                        shapes2[counter] = matchingShape;
                        shapeIDs[counter] = sh1.Id;
                        counter++;
                    }
                }
                return flag;
            }
            catch (Exception e)
            {
                LogException(e, "GetMatchingShapeDetails");
                throw;
            }
        }
        private float GetDistanceBetweenShapes(PowerPoint.Shape sh1, PowerPoint.Shape sh2)
        {
            float sh1CenterX = (sh1.Left + (sh1.Width / 2));
            float sh2CenterX = (sh2.Left + (sh2.Width / 2));
            float sh1CenterY = (sh1.Top + (sh1.Height / 2));
            float sh2CenterY = (sh2.Top + (sh2.Height / 2));
            float distSquared = (float)(Math.Pow((sh2CenterX - sh1CenterX), 2) +  Math.Pow((sh2CenterY - sh1CenterY), 2));
            return (float)(Math.Sqrt(distSquared));
        }
        private float GetMinimumRotation(float fromAngle, float toAngle)
        {
            try
            {
                fromAngle = Normalize(fromAngle);
                toAngle = Normalize(toAngle);

                float rotation1 = toAngle - fromAngle;
                float rotation2 = rotation1 == 0.0f ? 0.0f : Math.Abs(360.0f - Math.Abs(rotation1)) * (rotation1 / Math.Abs(rotation1)) * -1.0f;

                if (Math.Abs(rotation1) < Math.Abs(rotation2))
                {
                    return rotation1;
                }
                else
                {
                    return rotation2;
                }
            }
            catch (Exception e)
            {
                LogException(e, "GetMinimumRotation");
                throw;
            }
        }
        private float Normalize(float i)
        {
            try
            {
                //find effective angle
                float d = Math.Abs(i) % 360.0f;

                if (i < 0)
                {
                    //return positive equivalent
                    return 360.0f - d;
                }
                else
                {
                    return d;
                }
            }
            catch (Exception e)
            {
                LogException(e, "Normalize");
                throw;
            }
        }
        private bool DeleteShapeAnnimations(PowerPoint.Slide slide, PowerPoint.Shape shape)
        {
            try
            {
                PowerPoint.Sequence sequence = slide.TimeLine.MainSequence;
                bool flag = true;
                for (int x = sequence.Count; x >= 1; x--)
                {
                    PowerPoint.Effect effect = sequence[x];
                    if (effect.Shape.Name == shape.Name && effect.Shape.Id == shape.Id)
                    {
                        if (effect.Exit == Office.MsoTriState.msoTrue)
                            flag = false;
                        effect.Delete();
                    }
                }
                return flag;
            }
            catch (Exception e)
            {
                LogException(e, "DeleteShapeAnimations");
                throw;
            }
        }
        private bool HasEntryAnimation(PowerPoint.Slide slide, PowerPoint.Shape shape)
        {
            try
            {
                PowerPoint.Sequence sequence = slide.TimeLine.MainSequence;
                bool flag = false;
                for (int x = sequence.Count; x >= 1; x--)
                {
                    PowerPoint.Effect effect = sequence[x];
                    if (effect.Shape.Name == shape.Name && effect.Shape.Id == shape.Id)
                    {
                        if (entryEffects.Contains(effect.EffectType))
                        {
                            flag = true;
                            break;
                        }
                    }
                }
                return flag;
            }
            catch (Exception e)
            {
                LogException(e, "HasEntryAnimation");
                throw;
            }
        }
        private bool HasExitAnimation(PowerPoint.Slide slide, PowerPoint.Shape shape)
        {
            try
            {
                PowerPoint.Sequence sequence = slide.TimeLine.MainSequence;
                PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
                bool flag = false;
                for (int x = sequence.Count; x >= 1; x--)
                {
                    PowerPoint.Effect effect = sequence[x];
                    if (effect.Shape.Name == shape.Name && effect.Shape.Id == shape.Id)
                    {
                        if (effect.Exit == Office.MsoTriState.msoTrue)
                        {
                            flag = true;
                            break;
                        }
                    }
                }

                return flag;
            }
            catch (Exception e)
            {
                LogException(e, "HasEntryAnimation");
                throw;
            }
        }
        private void MoveMotionAnimation(PowerPoint.Slide slide)
        {
            try
            {
                PowerPoint.Sequence sequence = slide.TimeLine.MainSequence;
                PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
                foreach (PowerPoint.Effect eff in slide.TimeLine.MainSequence)
                {
                    if ((eff.EffectType >= PowerPoint.MsoAnimEffect.msoAnimEffectPathCircle && eff.EffectType <= PowerPoint.MsoAnimEffect.msoAnimEffectPathRight) || eff.EffectType == PowerPoint.MsoAnimEffect.msoAnimEffectCustom)
                    {
                        //sh.Delete();
                        PowerPoint.AnimationBehavior motion = eff.Behaviors[1];
                        if (motion.Type == PowerPoint.MsoAnimType.msoAnimTypeMotion)
                        {
                            PowerPoint.Shape sh = eff.Shape;
                            string motionPath = motion.MotionEffect.Path.Trim();
                            if (motionPath.Last() < 'A' || motionPath.Last() > 'Z')
                                motionPath += " X";
                            string[] path = motionPath.Split(' ');
                            int count = path.Length;
                            float xVal = Convert.ToSingle(path[count - 3]);
                            float yVal = Convert.ToSingle(path[count - 2]);
                            sh.Left += (xVal * presentation.PageSetup.SlideWidth);
                            sh.Top += (yVal * presentation.PageSetup.SlideHeight);
                        }
                    }
                }
            }
            catch (Exception e)
            {
            }
        }
        //Other Helpers
        private void AddAckSlide()
        {
            try
            {
                PowerPoint.Slide tempSlide = (Globals.ThisAddIn.Application.ActivePresentation.Slides[Globals.ThisAddIn.Application.ActivePresentation.Slides.Count]);
                if (!(tempSlide.Name.Contains("PPAck") && tempSlide.Name.Substring(0, 5).Equals("PPAck")))
                {
                    PowerPoint.Slide ackSlide = Globals.ThisAddIn.Application.ActivePresentation.Slides.AddSlide(Globals.ThisAddIn.Application.ActivePresentation.Slides.Count + 1, GetCurrentSlide().CustomLayout);
                    //Globals.ThisAddIn.Application.ActiveWindow.View.GotoSlide(ackSlide.SlideIndex);
                    PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
                    String tempFileName = Path.GetTempFileName();
                    Properties.Resources.Acknowledgement.Save(tempFileName);
                    float width = presentation.PageSetup.SlideWidth * 0.858f;
                    float height = presentation.PageSetup.SlideHeight * (5.33f / 7.5f);
                    PowerPoint.Shape ackShape = ackSlide.Shapes.AddPicture(tempFileName, Office.MsoTriState.msoFalse, Office.MsoTriState.msoTrue, ((presentation.PageSetup.SlideWidth - width) / 2), ((presentation.PageSetup.SlideHeight - height) / 2), width, height);
                    ackSlide.SlideShowTransition.Hidden = Office.MsoTriState.msoTrue;
                    ackSlide.Name = "PPAck" + GetTimestamp(DateTime.Now);
                }
            }
            catch (Exception e)
            {
                LogException(e, "AddAckSlide");
                throw;
            }
        }
        private void Log(string logText, string type)
        {
            if (type.Equals("Info"))
                Trace.TraceInformation(DateTime.Now.ToString("yyyyMMddHHmmss") + ": " + logText);
            else if (type.Equals("Error"))
                Trace.TraceError(DateTime.Now.ToString("yyyyMMddHHmmss") + ": " + logText);
            else if (type.Equals("Warning"))
                Trace.TraceWarning(DateTime.Now.ToString("yyyyMMddHHmmss") + ": " + logText);
        }
        private void LogException(Exception e, string methodName)
        {
            Log(methodName + ": " + e.Message + ": " + e.StackTrace, "Error");
        }
        private PowerPoint.Slide GetCurrentSlide()
        {
            try
            {
                PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
                return Globals.ThisAddIn.Application.ActiveWindow.View.Slide;
            }
            catch (Exception e)
            {
                LogException(e, "GetCurrentSlide");
                throw;
            }
        }
        private PowerPoint.Slide GetNextSlide(PowerPoint.Slide currentSlide)
        {
            try
            {
                PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
                int slideIndex = currentSlide.SlideIndex;
                return presentation.Slides[slideIndex + 1];
            }
            catch (Exception e)
            {
                LogException(e, "GetNextSlide");
                throw;
            }
        }
        private PowerPoint.Slide GetPrevSlide(PowerPoint.Slide currentSlide)
        {
            try
            {
                PowerPoint.Presentation presentation = Globals.ThisAddIn.Application.ActivePresentation;
                int slideIndex = currentSlide.SlideIndex;
                return presentation.Slides[slideIndex - 1];
            }
            catch (Exception e)
            {
                LogException(e, "GetPrevSlide");
                throw;
            }
        }
        private bool haveSameNames(PowerPoint.Shape sh1, PowerPoint.Shape sh2)
        {
            try
            {
                String name1 = sh1.Name;
                String name2 = sh2.Name;

                if (name1.ToUpper().CompareTo(name2.ToUpper()) == 0)
                    return true;
                else
                    return false;
            }
            catch (Exception e)
            {
                LogException(e, "haveSameNames");
                throw;
            }
        }
        public String GetTimestamp(DateTime value)
        {
            try
            {
                return value.ToString("yyyyMMddHHmmssffff");
            }
            catch (Exception e)
            {
                LogException(e, "GetTimestamp");
                throw;
            }
        }

        private static string GetResourceText(string resourceName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string[] resourceNames = asm.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; ++i)
            {
                if (string.Compare(resourceName, resourceNames[i], StringComparison.OrdinalIgnoreCase) == 0)
                {
                    using (StreamReader resourceReader = new StreamReader(asm.GetManifestResourceStream(resourceNames[i])))
                    {
                        if (resourceReader != null)
                        {
                            return resourceReader.ReadToEnd();
                        }
                    }
                }
            }
            return null;
        }

        #endregion
    }
}
