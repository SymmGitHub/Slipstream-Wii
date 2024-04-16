# Credits
Symm - Main Programmer and UI Design

Wiimm - Creator of SZS tools used in this program

# Slipstream Wii
Slipstream Wii is used to create (.sample.szs) files, special (.szs) files that store models, animations, vehicles, textures, and the name of a driver in Mario Kart Wii. Using extracted MKW files, Slipstream Wii can create character samples which can be edited through a SZS editor like Brawlbox or Brawlcrate. With one or more edited sample files, Slipstream Wii can turn the sample files back into a collection of edited Mario Kart Wii files which can be put back into the game through programs like Riivolution.

# Setup
Before doing anything, you should first have a rom/iso of MKW which you can obtain legally using CleanRip and a modded Wii. From there, you'll need to extract its files using Dolphin Emulator. If Mario Kart Wii is already in your list of games in Dolphin, just right-click it, select 'Properties', go to the 'Filesystem' tab on the far right, right-click the the item labelled 'Data Partition (0)' (Or whichever partition that has the Race, Demo, and Scene folders in it) and then hit 'Extract Files...' to open up a window and choose which folder you extract to. For convenience, you should create a new folder by SlipstreamWii.exe and extract the files there.

Open up Slipstream Wii, hit 'File > Set MKW Files Folder Path' and you can select the folder containing mkw's files, and it'll use that folder for pulling vanilla files from.

# Creating Sample Files
To create a sample file for the character you want to use as a base, select the target character at the top-right corner of the tool and hit the 'Create Sample SZS' button. Choose any filename for your sample and after a few seconds, a new sample model will be created using the extracted mkw files as a base. The three princesses in the game: Peach, Daisy, and Rosalina, are defined as 'Complex' characters due to their unique bike outfit models and Slipstream Wii may prompt you to switch to 'Complex Sampling'. Complex Samples are roughly the same as normal, but have unique models and textures for each vehicle type alongside a unique main menu model. The 'Simple' samples used by default only use one model for all of these instances, though each vehicle type has it's own low-poly, LOD model.

# Editing Sample Files
With your sample files, Open one up in Brawlbox or Brawlcrate and you can start editing it. For simple samples, the main model, low-poly LOD models, and textures can be found in 'driver.brres'. with the other brres files nearby storing animations. For the aforementioned complex samples, 'driving_(kart/bike/sport_bike).brres' and 'select_char.brres' store models and textures instead. The Icons a character use are stored below as (.tpl) files. If you want to change what name the game uses. Open the 'name' folder inside the szs and right click the character's name, hit 'Rename' and enter whatever new name you want the character to be. underneath is the 'vehicles' folder which stores the models, textures, and animations of each vehicle. For whatever reason, specifically the Mach Bike uses unique tire textures for the main menu, so it has two texture folders for each.

# Creating Files From a Sample
To create modified mkw files using a sample, press the button fittingly labelled 'Create Files From Sample'. Feel free to edit the list of files to create in case you don't want to bother making certain szs files.

You can actually select multiple samples to create files from, this is good for instances where multiple characters share a single file like Award Ceremony models or Character Selection models. But when creating multiple samples, instead of using a target character, Slipstream Wii goes by character name to determine what character to import a sample over. Just remember to rename Samples to match the character you want to replace, substituting spaces for dashes. (i.e. Mario.sample.szs or Funky-Kong.sample.szs)

With modified files, you can now drag them back into the game using Riivolution and play the game with your new character!
