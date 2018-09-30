******************
* Session Slicer *
******************

1.0 PURPOSE

The purpose of this application is to take an already sliced model gCode file and break it up into multiple files which each contain startup and end code.
This allows a large print to be broken into a number sessions. Each session has independent start and end code for printing its section and thus the printer
can be turned off after any (or all) sessions and the next session will allow the print to continue at a later time (assuming the print is not removed from
the print bed).


2.0 USAGE

Running that application with only the name of the gCode file will cause the application to show the correct syntax but it will also identify the maximum
height of the print object (in the gCode file). This can help decide at what heights to make session slices since the application sessions are determined by
height slices and not time slices.

It should be kept in mind that height of the slice may not necessarily be proportional to the time slice. For example, if a cone or pyramid object is sliced
a the half height point, the first session would take much longer to print than the second session (due to the tapering shape).

To actually session slice a gCode file, add additional arguments indicating at what heights each new session should begin. If multiple slice heights are
provided they must be provided in order from smallest to largest.

The resulting session files will be created based on the original gCode file name but the original gCode file will not be modified in any way.

The configuration, as is, has been tested with the output of CuraEngine slicer from Repetier (v 2.1.3) software. Configurastion adjustments may be necessary
with other slicers but even the automatic functions should work with most slicer outputs. However, in absolute worst case, all of the automatic functions
can be disabled and the start/end code can be provided manually (see next section).


3.0 CONFIGURATION

The session breaking is confiured to be as generic as possible but tweaks may be necessary to get the application to work with the gCode output of your
favorite slicing software.

To allow a future session to restart the application copies a number of lines, from the beginning of the original gCode, to each session file's start.
The number of lines copied can be configured manually in the configuration file using the key:

<configuration>
  <appSettings>      
    <add key="startCodeLines" value="-1" />
  </appSettings>
</configuration>

If set manually to 0 or a positive number, that many lines from the beginning of the original gCode file will be copied to form the start of additional
session files. If the configured value is negative, the application will try to auto detect the number of lines to be copied. It does this by copying
all lines until the first G0 or G1 command that includes an X, Y and Z value.

In addition to the copied lines, any additional code can be inserted at the start of each additional session by configuring the key:

<configuration>
  <appSettings>      
    <add key="startCode" value="; Additional Start Code" />
  </appSettings>
</configuration>

The value of the configuration is added after the copied lines. A few characters and character sequences have special meaning in this case. The pipe
character (|) is used to seperate multiple commands. The {S} sequence is replaced with the current session two digit number. The {H} sequence is
replaced with the current height. Lastly the {H+} sequence is replaced with the current height plsu 5. The last is typically used when moving the
extrude to or away from the print.

When a session is completed a similar process is used to process the end commands. A number of lines are copied from the end of the original gCode file
to the end of each session except the last (which will already contain the orignal set). The number of lines to be copied can be set manually using
the configuration file key:

<configuration>
  <appSettings>      
    <add key="endCodeLines" value="-1" />
  </appSettings>
</configuration>

If set manually to 0 or a positive number, that many lines from the end of the original gCode file will be copied to form the end of all sessions except
the last. If the configured value is negative, the application will try to auto detect the number of lines to be copied. It does this by determing the
last draw G0 or G1 code and assuming all additional lines are ending line. The last draw line is determined to the last G0 or G1 command that has an
X and a Y component but not a Z component.

In addition to the copied lines, any additional code can be inserted at the end of each session (except the last) by configuring the key:

<configuration>
  <appSettings>      
    <add key="endCode" value="; Additional End Code" />
  </appSettings>
</configuration>

The value of the configuration is added after the copied lines. A few characters and character sequences have special meaning in this case. The pipe
character (|) is used to seperate multiple commands. The {S} sequence is replaced with the current session two digit number. The {H} sequence is
replaced with the current height. Lastly the {H+} sequence is replaced with the current height plsu 5. The last is typically used when moving the
extrude to or away from the print.

Lastly the application provides a backfill feature which can be turned on or off. The backfill feature has 4 settings:

0 = Off
1 = Comments only
2 = "G? F*" Commands only
3 = Comments and "G? F*" commands

When the backfill optionm is turned on, when a new session starts, the application will go back 5 steps and inlcude in the new session start up any lines
that match the given backfill mode. If only the comments option is turned on, this allows any comments before the session break (typically layer comments)
to be placed into the new session file. If the "G? F*" option is turned on, it will also place any G0 or G1 commands that include the F parameter into
the new session file. This is typically not necessary because the G0 or G1 height adjustment commands typically include the F component but this option
is avaialble is case is does not.
