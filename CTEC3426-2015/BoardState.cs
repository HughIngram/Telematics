using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTEC3426_2015
{
    public class BoardState
    {
        public enum MotorDirection
        {
            FORWARD, REVERSE
        }

        private static int N_LEDS = 4;
        private static int N_KP_BUTTONS = 12;
        private static int N_SWITCHES = 4;

        public Boolean isFanOn = false;
        public MotorDirection motorDirection = MotorDirection.FORWARD;
        public Boolean isHeaterOn = false;
        public String temperature = "";
        public Boolean[] ledArray = new Boolean[N_LEDS];
        public Boolean[] keypad = new Boolean[N_KP_BUTTONS]; // order of items: 0123456789*#
        public Boolean[] switches = new Boolean[N_SWITCHES];

        // default constructor
        public BoardState()
        { }

        // copy constructor
        public BoardState(BoardState previousState)
        {
            isFanOn = previousState.isFanOn;
            motorDirection = previousState.motorDirection;
            isHeaterOn = previousState.isHeaterOn;
            temperature = previousState.temperature;
            Array.Copy(previousState.ledArray, ledArray, N_LEDS);
            Array.Copy(previousState.keypad, keypad, N_KP_BUTTONS);
            Array.Copy(previousState.switches, switches, N_SWITCHES);
        }

        override
        public String ToString()
        {
            return "(\"temp\":\"" + temperature + "\""
                + ",\"heater\":\"" + getOnOrOff(isHeaterOn) + "\""
                + ",\"motor\":\"" + motorToString() + "\""
                + ",\"leds\":<"
                    + "\"" + getOnOrOff(ledArray[0]) + "\""
                    + ",\"" + getOnOrOff(ledArray[1]) + "\""
                    + ",\"" + getOnOrOff(ledArray[2]) + "\""
                    + ",\"" + getOnOrOff(ledArray[3]) + "\">"
                + ",\"switches\":<"
                    + ",\"" + getOnOrOff(switches[0]) + "\""
                    + ",\"" + getOnOrOff(switches[1]) + "\""
                    + ",\"" + getOnOrOff(switches[2]) + "\""
                    + ",\"" + getOnOrOff(switches[3]) + "\">"
                + ",\"keypad\":\"" + getActiveKeypadButton() + "\""
                + ")";
        }

        public static String getOnOrOff(Boolean b)
        {
            if (b)
            {
                return "ON";
            } else
            {
                return "OFF";
            }
        }

        public String motorToString()
        {
            if (isFanOn)
            {
                if (motorDirection == MotorDirection.FORWARD)
                {
                    return "FORWARD";
                } else
                {
                    return "REVERSE";
                }
            } else
            {
                return "OFF";
            }
        }

        private String getActiveKeypadButton()
        {
            // this implementation assumes only one button can be pressed at a time.
            String button = "NULL";
            for (int i = 0; i < 10; i++)
            {
                if (keypad[i])
                {
                    button = i.ToString();
                }
            }
            if (keypad[10])
            {
                button = "*";
            } else if (keypad[11])
            {
                button = "#";
            }
            return button;
        }

    }

}
