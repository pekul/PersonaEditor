﻿using PersonaEditorLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersonaEditorGUI.Controls.HexEditor
{
    class ScrollBarVM : BindingObject
    {
        public delegate void ValueChangedHandler(double value);
        public event ValueChangedHandler ValueChanged;

        private double minimum = 0;
        public double Minimum
        {
            get { return minimum; }
            set
            {
                if (value != minimum)
                {
                    minimum = value;
                    Notify("Minimum");
                }
            }
        }

        private double maximum = 0;
        public double Maximum
        {
            get { return maximum; }
            set
            {
                if (value != maximum)
                {
                    maximum = value;
                    Notify("Maximum");
                }
            }
        }

        private double value = 0;
        public double Value
        {
            get { return value; }
            set
            {
                if (this.value != value)
                {
                    this.value = value;
                    ValueChanged?.Invoke(value);
                    Notify("Value");
                }
            }
        }

        public void SetValue(bool add)
        {
            if (add)
            {
                if (value + 4 <= maximum)
                    Value += 4;
                else
                    Value = maximum;
            }
            else
            {
                if (value - 4 >= minimum)
                    Value -= 4;
                else
                    Value = minimum;
            }
        }
    }
}