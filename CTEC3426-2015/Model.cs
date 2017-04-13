using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTEC3426_2015
{
    /**
        The 'business' logic for the code.
        No UI code in here.
    **/
    class ApplicationModel
    {
        private CTEC3426 form;

        public String broadcastId = "";

        // the state of the remote board
        public BoardState remoteBoardState = new BoardState();

        public ApplicationModel(CTEC3426 form)
        {
            this.form = form;
        }

        public void updateRemoteBoardState(String[] data)
        {
            // last entry in data[] is an esc char
            byte[] bytes = new byte[data.Length - 1];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(data[i], System.Globalization.NumberStyles.HexNumber); 
            }

            // read the temperature
            remoteBoardState.temperature = bytes[2] + "." + bytes[3];

            // read the heater status
            // we need the fourth bit of the zeroth byte
            int heaterBitNumber = 4;
            remoteBoardState.isHeaterOn = (bytes[0] & (1 << heaterBitNumber)) != 0;

            // read the fan status
            // note that the fan status is stored differently for incoming / outgoing
            int motorForwardBitNumber = 5;
            Boolean isMotorOnForward = (bytes[0] & (1 << motorForwardBitNumber)) != 0;
            int motorBackwardBitNumber = 6;
            Boolean isMotorOnBackward = (bytes[0] & (1 << motorBackwardBitNumber)) != 0;
            remoteBoardState.isFanOn = isMotorOnForward || isMotorOnBackward;
            if (isMotorOnForward)
            {
                remoteBoardState.motorDirection = BoardState.MotorDirection.FORWARD;
            }
            if (isMotorOnBackward)
            {
                remoteBoardState.motorDirection = BoardState.MotorDirection.REVERSE;
            }

            // read the status of the LED's
            // bits 0 -> 3 of byte 5.
            for (int ledNumber = 0; ledNumber < remoteBoardState.ledArray.Length; ledNumber++)
            {
                Boolean isLedOn = (bytes[4] & (1 << ledNumber)) != 0;
                remoteBoardState.ledArray[ledNumber] = isLedOn;
            }

            // read the status of the keypad from byte 1
            // characters 0 -> 9 are encoded as ascii
            int character = Convert.ToByte('0');
            for (int i = 0; i < 10; i++)
            {
                remoteBoardState.keypad[i] = (character + i) == bytes[1];
            }
            // * button
            remoteBoardState.keypad[10] = bytes[1] == 0x53;
            // # button
            remoteBoardState.keypad[11] = bytes[1] == 0x48;

            // read the status of the switches
            // bits 0->3 of byte 0
            for (int switchNumber = 0; switchNumber < remoteBoardState.switches.Length; switchNumber++)
            {
                remoteBoardState.switches[switchNumber] = (bytes[0] & (1 << switchNumber)) != 0;
            }
        }

        public void setUpMask(String mask)
        {
            form.sendCommand(form.serialPort, "M", mask);  
        }

        public void setUpIncomingFilter(String id)
        {
            form.sendCommand(form.serialPort, "A", id);
        }

        public void setUpBroadcastId(String id)
        {
            broadcastId = id;
        }

        public void toggleHeater()
        {
            BoardState desiredState = new BoardState(remoteBoardState);
            desiredState.isHeaterOn = !remoteBoardState.isHeaterOn;
            sendAllCommands(desiredState);
        }

        public void fanForward()
        {
            BoardState desiredState = new BoardState(remoteBoardState);
            desiredState.isFanOn = true;
            desiredState.motorDirection = BoardState.MotorDirection.FORWARD;
            sendAllCommands(desiredState);
        }

        public void fanReverse()
        {
            BoardState desiredState = new BoardState(remoteBoardState);
            desiredState.isFanOn = true;
            desiredState.motorDirection = BoardState.MotorDirection.REVERSE;
            sendAllCommands(desiredState);
        }

        public void fanOff()
        {
            BoardState desiredState = new BoardState(remoteBoardState);
            desiredState.isFanOn = false;
            sendAllCommands(desiredState);
        }

        /*
        String values passed to this method must be decimals to 1dp.
        e.g.: "35.0", "27.1"
        */
        public void tempSetPoint(String setPoint)
        {

            BoardState desiredState = new BoardState(remoteBoardState);
            if (remoteBoardState.temperature.Equals(setPoint))
            {
                // do nothing if the temperature is correct.
                desiredState.isFanOn = false;
                desiredState.isHeaterOn = false;
            } else if (Double.Parse(remoteBoardState.temperature) > Double.Parse(setPoint))
            {
                // actual temperature is higher than desired
                desiredState.isFanOn = true;
                desiredState.motorDirection = BoardState.MotorDirection.FORWARD;
                desiredState.isHeaterOn = false;
            } else
            {
                // actual temperature is lower than desired
                desiredState.isFanOn = false;
                desiredState.isHeaterOn = true;
            }
            // send the command. Will this break due to being too fast??
            sendAllCommands(desiredState);
        }

        /*
        Set the desired on / off state of the 4 LEDs.
        @param ledValues should be an array of size 4.
        */
        public void setLeds(Boolean[] ledValues)
        {
            BoardState desiredState = new BoardState(remoteBoardState);
            for (int i = 0; i < 4; i++)
            {
                desiredState.ledArray[i] = ledValues[i];
            }
            sendAllCommands(desiredState);
        }

        public String getStatusString()
        {
            return remoteBoardState.ToString();
        }

        public void sendSms(String number, String message)
        {
            form.sendCommand(form.serialPort, "$", number + "#" + message + "\n");
        }

        /**
        * Sends a CAN message for the desired state of the board being controlled.
        * @param desiredState the state which is desired for the remote board
        **/
        public void sendAllCommands(BoardState desiredState)
        {
            // the zeroth byte of the CAN message being sent
            int byte0 = 0x00;

            // heater
            if (desiredState.isHeaterOn)
            {
                byte0 |= 0x01;
            }

            // fan
            if (desiredState.isFanOn)
            {
                byte0 |= 0x02;

                if (desiredState.motorDirection == BoardState.MotorDirection.REVERSE)
                {
                    byte0 |= 0x04;
                }
            }

            // LED's are controlled by bits 4 -> 7 of byte 0. 
            int ledFlag = 0x10; // LED 0
            for (int i = 0; i < 4; i++)
            {
                if (desiredState.ledArray[i])
                {
                    byte0 |= ledFlag;
                }
                ledFlag = ledFlag << 1; // move the flag to the next bit
            }
           
            if (desiredState.ledArray[0])
            {
                byte0 |= 0x10;
            }

            // pad with 14 zeroes.
            String commandOutput = broadcastId + byte0.ToString("X2") + "00000000000000";
            form.sendCommand(form.serialPort, "#", commandOutput);
        }

    }
}
